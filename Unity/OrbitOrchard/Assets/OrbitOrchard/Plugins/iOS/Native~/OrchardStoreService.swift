import StoreKit

/// Purchases change the orchard's appearance, never its scoring or difficulty.
/// The original Plus product remains the entitlement so previous owners keep access.
@MainActor
final class OrchardStoreService {
    var onChange: (() -> Void)?
    nonisolated static let plusID = "com.flutterly.gravitile.plus"
    nonisolated static let tipIDs = [
        "com.flutterly.gravitile.tip.small",
        "com.flutterly.gravitile.tip.medium",
        "com.flutterly.gravitile.tip.large",
    ]

    enum ProductState: Equatable {
        case idle, loading, available, unavailable, failed
    }

    enum PurchaseState: Equatable {
        case idle, purchasing, pending, purchased, cancelled, failed
    }

    private(set) var isPlus = false { didSet { onChange?() } }
    var hasOrchardPass: Bool { isPlus }
    private(set) var plusProduct: Product? = nil { didSet { onChange?() } }
    private(set) var tipProducts: [Product] = [] { didSet { onChange?() } }
    private(set) var lastTipThanks = false { didSet { onChange?() } }
    private(set) var productState: ProductState = .idle { didSet { onChange?() } }
    private(set) var purchaseState: PurchaseState = .idle { didSet { onChange?() } }
    private(set) var isRestoring = false { didSet { onChange?() } }
    private(set) var statusMessage: String? = nil { didSet { onChange?() } }
    var isPurchasing: Bool { purchaseState == .purchasing }

    private var updatesTask: Task<Void, Never>?

    init(automaticallyLoad: Bool = true) {
        updatesTask = Task { [weak self] in
            for await update in Transaction.updates {
                guard !Task.isCancelled else { return }
                await self?.receive(update)
            }
        }
        if automaticallyLoad {
            Task { [weak self] in
                await self?.refreshEntitlements()
                await self?.loadProducts()
            }
        }
    }

    deinit { updatesTask?.cancel() }

    func loadProducts() async {
        guard productState != .loading else { return }
        productState = .loading
        do {
            let products = try await Product.products(for: [Self.plusID] + Self.tipIDs)
            plusProduct = products.first { $0.id == Self.plusID && $0.type == .nonConsumable }
            tipProducts = products
                .filter { Self.tipIDs.contains($0.id) && $0.type == .consumable }
                .sorted { $0.price < $1.price }
            productState = plusProduct == nil ? .unavailable : .available
            if plusProduct == nil {
                statusMessage = "Existing purchases are unavailable in the App Store right now. Try again later."
            } else if purchaseState == .idle {
                statusMessage = nil
            }
        } catch {
            productState = .failed
            statusMessage = "Couldn't reach the App Store. Check your connection and try again."
        }
    }

    func ensureProductsLoaded() async {
        guard plusProduct == nil else { return }
        await loadProducts()
    }

    @discardableResult
    func purchase(_ product: Product) async -> Bool {
        guard !isPurchasing, !isRestoring,
              product.id == Self.plusID || Self.tipIDs.contains(product.id) else { return false }
        purchaseState = .purchasing
        lastTipThanks = false
        statusMessage = nil
        do {
            switch try await product.purchase() {
            case let .success(verification):
                guard case let .verified(transaction) = verification else {
                    purchaseState = .failed
                    statusMessage = "Apple couldn't verify this purchase. Your progress is unchanged. Try Restore Purchases."
                    return false
                }
                await grantAndFinish(transaction)
                return true
            case .pending:
                purchaseState = .pending
                statusMessage = "Purchase awaiting approval. Apple will confirm when it is ready."
                return false
            case .userCancelled:
                purchaseState = .cancelled
                statusMessage = nil
                return false
            @unknown default:
                purchaseState = .failed
                statusMessage = "This purchase couldn't be completed. Please try again."
                return false
            }
        } catch {
            purchaseState = .failed
            statusMessage = "The purchase didn't complete. Check your connection and try again."
            return false
        }
    }

    /// Only called from Restore Purchases; sync may ask for Apple ID authentication.
    func restore() async {
        guard !isRestoring, !isPurchasing else { return }
        isRestoring = true
        statusMessage = nil
        defer { isRestoring = false }
        do {
            try await AppStore.sync()
            await refreshEntitlements()
            statusMessage = isPlus
                ? "Your existing purchase ownership is restored."
                : "No existing purchase was found for this Apple Account."
        } catch {
            statusMessage = "Purchases couldn't be restored. Check your connection and try again."
        }
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

    private func receive(_ result: VerificationResult<Transaction>) async {
        guard case let .verified(transaction) = result else {
            statusMessage = "A purchase could not be verified by Apple. Try Restore Purchases."
            return
        }
        guard transaction.productID == Self.plusID || Self.tipIDs.contains(transaction.productID) else { return }
        await grantAndFinish(transaction)
    }

    private func grantAndFinish(_ transaction: Transaction) async {
        await refreshEntitlements()
        if transaction.revocationDate != nil {
            purchaseState = .idle
            statusMessage = "This purchase is no longer active."
        } else if Self.tipIDs.contains(transaction.productID) {
            lastTipThanks = true
            purchaseState = .purchased
            statusMessage = "Thank you for supporting this little orchard."
        } else {
            purchaseState = .purchased
            statusMessage = "Carriage collection unlocked. Your new finishes are ready."
        }
        // Finish only verified, supported transactions after delivering the entitlement.
        await transaction.finish()
    }
}

