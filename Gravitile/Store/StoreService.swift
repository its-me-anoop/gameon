import StoreKit

/// StoreKit 2 storefront. One non-consumable (Gravitile Plus) plus tip-jar
/// consumables. Entitlements come from `Transaction.currentEntitlements`, so
/// purchases survive reinstalls and family sharing works for free.
@Observable @MainActor
final class StoreService {
    nonisolated static let plusID = "com.flutterly.gravitile.plus"
    nonisolated static let tipIDs = [
        "com.flutterly.gravitile.tip.small",
        "com.flutterly.gravitile.tip.medium",
        "com.flutterly.gravitile.tip.large",
    ]

    private(set) var isPlus = false
    private(set) var plusProduct: Product?
    private(set) var tipProducts: [Product] = []
    private(set) var lastTipThanks = false

    private var updatesTask: Task<Void, Never>?

    init() {
        updatesTask = Task { [weak self] in
            for await update in Transaction.updates {
                if case let .verified(transaction) = update {
                    await transaction.finish()
                }
                await self?.refreshEntitlements()
            }
        }
        Task {
            await loadProducts()
            await refreshEntitlements()
        }
    }

    func loadProducts() async {
        let ids = [Self.plusID] + Self.tipIDs
        guard let products = try? await Product.products(for: ids) else { return }
        plusProduct = products.first { $0.id == Self.plusID }
        tipProducts = products
            .filter { Self.tipIDs.contains($0.id) }
            .sorted { $0.price < $1.price }
    }

    /// Called from purchase surfaces: retries the fetch (with backoff) if the
    /// launch-time load failed, so the paywall never strands the user with a
    /// dead buy button after a transient network failure.
    func ensureProductsLoaded() async {
        for attempt in 0..<5 {
            if plusProduct != nil && !tipProducts.isEmpty { return }
            if attempt > 0 {
                try? await Task.sleep(for: .seconds(Double(attempt)))
            }
            await loadProducts()
        }
    }

    @discardableResult
    func purchase(_ product: Product) async -> Bool {
        guard let result = try? await product.purchase() else { return false }
        switch result {
        case let .success(verification):
            if case let .verified(transaction) = verification {
                await transaction.finish()
                if Self.tipIDs.contains(transaction.productID) {
                    lastTipThanks = true
                }
            }
            await refreshEntitlements()
            return true
        case .userCancelled, .pending:
            return false
        @unknown default:
            return false
        }
    }

    func restore() async {
        try? await AppStore.sync()
        await refreshEntitlements()
    }

    func refreshEntitlements() async {
        var plus = false
        for await entitlement in Transaction.currentEntitlements {
            if case let .verified(transaction) = entitlement,
               transaction.productID == Self.plusID,
               transaction.revocationDate == nil {
                plus = true
            }
        }
        isPlus = plus
    }
}

/// What Plus unlocks, in one place so gating stays consistent and testable.
enum Entitlements {
    static func maxUndosPerGame(isPlus: Bool) -> Int {
        isPlus ? Int.max : 1
    }

    static func canPlayArchive(isPlus: Bool) -> Bool {
        isPlus
    }
}
