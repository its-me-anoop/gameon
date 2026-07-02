import Testing
import Foundation
@testable import Gravitile

private final class ConfigBundleToken {}

/// Guards against drift between the .storekit configuration (which mirrors
/// what gets created in App Store Connect) and the product IDs the code uses.
@Suite struct StoreConfigurationTests {
    private func configuration() throws -> [String: Any] {
        let url = Bundle(for: ConfigBundleToken.self)
            .url(forResource: "Gravitile", withExtension: "storekit")!
        let data = try Data(contentsOf: url)
        return try #require(try JSONSerialization.jsonObject(with: data) as? [String: Any])
    }

    @Test func configurationContainsExactlyTheProductsCodeExpects() throws {
        let config = try configuration()
        let products = try #require(config["products"] as? [[String: Any]])
        let ids = Set(products.compactMap { $0["productID"] as? String })
        #expect(ids == Set([StoreService.plusID] + StoreService.tipIDs))
    }

    @Test func plusIsNonConsumableAndFamilyShareable() throws {
        let config = try configuration()
        let products = try #require(config["products"] as? [[String: Any]])
        let plus = try #require(products.first { $0["productID"] as? String == StoreService.plusID })
        #expect(plus["type"] as? String == "NonConsumable")
        #expect(plus["familyShareable"] as? Bool == true)
    }

    @Test func tipsAreConsumables() throws {
        let config = try configuration()
        let products = try #require(config["products"] as? [[String: Any]])
        for tipID in StoreService.tipIDs {
            let tip = try #require(products.first { $0["productID"] as? String == tipID })
            #expect(tip["type"] as? String == "Consumable")
        }
    }
}
