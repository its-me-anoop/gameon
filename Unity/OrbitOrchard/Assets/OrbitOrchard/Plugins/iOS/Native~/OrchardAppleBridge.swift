import Foundation
import StoreKit
import GameKit
import UIKit

/// A native service plugin only. Unity owns the scene, game loop, input, and UI.
@MainActor @objc(OrchardAppleBridge)
public final class OrchardAppleBridge: NSObject, @preconcurrency GKGameCenterControllerDelegate {
    @objc public static let shared = OrchardAppleBridge()
    @objc public var eventHandler: ((String) -> Void)?

    private let store = OrchardStoreService(automaticallyLoad: false)
    private let gameCenter = OrchardGameCenterService()
    private let catchFeedback = UIImpactFeedbackGenerator(style: .light)
    private let bankFeedback = UIImpactFeedbackGenerator(style: .medium)
    private let missFeedback = UINotificationFeedbackGenerator()
    private var initialized = false

    private override init() {
        super.init()
        store.onChange = { [weak self] in self?.emit(source: "store") }
        gameCenter.onChange = { [weak self] in self?.emit(source: "gameCenter") }
        NotificationCenter.default.addObserver(self, selector: #selector(accessibilityDidChange),
            name: UIAccessibility.reduceMotionStatusDidChangeNotification, object: nil)
    }

    @objc private func accessibilityDidChange() { emit(source: "accessibility") }

    /// Called only for a player's enabled gameplay feedback preference.
    @objc public func playHaptic(_ kind: Int32) {
        switch kind {
        case 0: catchFeedback.impactOccurred()
        case 1: bankFeedback.impactOccurred()
        case 2: missFeedback.notificationOccurred(.warning)
        default: break
        }
    }

    @objc public func initialize() {
        emit(source: "initial")
        guard !initialized else { return }
        initialized = true
        Task {
            await store.refreshEntitlements()
            await store.loadProducts()
            emit(source: "store")
        }
    }

    @objc public func loadProducts() {
        Task { await store.loadProducts(); emit(source: "store") }
    }

    @objc public func purchase(_ productID: String) {
        guard let product = ([store.plusProduct].compactMap { $0 } + store.tipProducts)
            .first(where: { $0.id == productID }) else {
            emit(source: "store", message: "This product is unavailable. Refresh the shop and try again.")
            return
        }
        Task { await store.purchase(product); emit(source: "store") }
    }

    @objc public func restorePurchases() {
        Task { await store.restore(); emit(source: "store") }
    }

    @objc public func authenticateGameCenter() {
        gameCenter.authenticate()
        emit(source: "gameCenter")
    }

    @objc public func submitScore(_ score: Int32, mode: String, dayKey: String) {
        guard let scoreMode = OrchardScoreMode(rawValue: mode) else {
            emit(source: "gameCenter", message: "This game mode does not support leaderboard scores.")
            return
        }
        gameCenter.submit(score: Int(score), mode: scoreMode, dayKey: dayKey.isEmpty ? nil : dayKey)
        emit(source: "gameCenter")
    }

    @objc public func retryScores() {
        Task {
            await store.refreshEntitlements()
            await gameCenter.retryPendingScores()
            emit(source: "gameCenter")
        }
    }

    /// Kept as a compatibility entry point; this build never opens the old arcade boards.
    @objc public func showLeaderboard(_ daily: Bool) { showWeeklyLeaderboard() }

    @objc public func useLifelineLeaderboards() { emit(source: "gameCenter") }

    @objc public func showWeeklyLeaderboard() {
        Task {
            guard await gameCenter.currentWeeklyLeaderboard() != nil else {
                emit(source: "gameCenter")
                return
            }
            guard let presenter = presenter() else {
                emit(source: "gameCenter", message: "The leaderboard could not open. Please try again.")
                return
            }
            let controller = GKGameCenterViewController(leaderboardID: OrchardGameCenterService.weeklyLeaderboardID,
                                                        playerScope: .global, timeScope: .allTime)
            controller.gameCenterDelegate = self
            presenter.present(controller, animated: true)
        }
    }

    public func gameCenterViewControllerDidFinish(_ gameCenterViewController: GKGameCenterViewController) {
        gameCenterViewController.dismiss(animated: true)
    }

    private func presenter() -> UIViewController? {
        var current = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .filter { $0.activationState == .foregroundActive }
            .flatMap { $0.windows }.first { $0.isKeyWindow }?.rootViewController
        while let presented = current?.presentedViewController { current = presented }
        return current
    }

    private func emit(source: String, message: String? = nil) {
        let products = ([store.plusProduct].compactMap { $0 } + store.tipProducts).map { product in
            ["id": product.id, "title": product.displayName, "description": product.description,
             "price": product.displayPrice, "isConsumable": product.type == .consumable] as [String: Any]
        }
        let storeStatus = store.statusMessage ?? ""
        let snapshot: [String: Any] = [
            "type": "state", "source": source,
            "isReduceMotionEnabled": UIAccessibility.isReduceMotionEnabled,
            "isPassOwned": store.hasOrchardPass,
            "isStoreLoading": store.productState == .loading,
            "isPurchasing": store.isPurchasing,
            "isRestoring": store.isRestoring,
            "productState": String(describing: store.productState),
            "purchaseState": String(describing: store.purchaseState),
            "storeStatus": storeStatus,
            "isGameCenterAuthenticated": gameCenter.isAuthenticated,
            "gameCenterState": String(describing: gameCenter.authenticationState),
            "playerDisplayName": gameCenter.playerDisplayName ?? "",
            "gameCenterStatus": gameCenter.statusMessage,
            "pendingScoreCount": gameCenter.queuedScoreCount,
            "dailyAvailability": "",
            "status": message ?? (source == "store" ? storeStatus : gameCenter.statusMessage),
            "products": products,
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: snapshot),
              let json = String(data: data, encoding: .utf8) else { return }
        eventHandler?(json)
    }
}
