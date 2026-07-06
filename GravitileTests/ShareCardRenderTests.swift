import Testing
import SwiftUI
import GravitileKit
@testable import Gravitile

@MainActor
@Suite struct ShareCardRenderTests {
    @Test func rendersTheCardAtItsDesignSize() {
        var game = GameState(mode: .endless, seed: 42)
        for direction in [Direction.left, .down, .right, .up, .left, .down] {
            _ = game.applyMove(direction)
        }
        let renderer = ImageRenderer(content: ShareCardView(model: ShareCardModel(game: game)))
        renderer.scale = 1
        let image = renderer.uiImage
        #expect(image != nil)
        #expect(image?.size.width == 360)
        #expect(image?.size.height == 450)
        // Visual-check artifact for development; harmless if the write fails.
        try? image?.pngData()?.write(to: URL(fileURLWithPath: "/tmp/gravitile-share-card.png"))
    }

    @Test func payloadCarriesTextAndImage() {
        var game = GameState(mode: .zen, seed: 7)
        _ = game.applyMove(.left)
        let payload = ShareCardRenderer.payload(for: game)
        #expect(payload.items.count == 2)
        #expect(payload.items.first is String)
        #expect((payload.items.first as? String)?.contains(ShareCard.appStoreURL) == true)
    }
}
