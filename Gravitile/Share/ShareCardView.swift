import SwiftUI
import GravitileKit

/// Everything a share card needs, buildable from a live game or a saved record.
struct ShareCardModel {
    let modeLabel: String
    let score: Int
    let bestTile: Int
    let cascadeCount: Int
    let deepestRound: Int
    let movesLine: String?
    let board: Board?

    init(game: GameState) {
        switch game.mode {
        case let .daily(number, budget):
            modeLabel = "Daily #\(number)"
            movesLine = "\(game.moveCount)/\(budget) moves"
        case .endless:
            modeLabel = "Endless"
            movesLine = nil
        case .zen:
            modeLabel = "Zen"
            movesLine = nil
        case let .sprint(budget):
            modeLabel = "Sprint"
            movesLine = "\(game.moveCount)/\(budget) moves"
        case .math:
            modeLabel = "Math Pop"
            movesLine = game.bondsCleared == 1 ? "1 bond" : "\(game.bondsCleared) bonds"
        }
        score = game.score
        bestTile = game.bestTile
        cascadeCount = game.cascadeCount
        deepestRound = game.bestCascadeRound
        board = game.board
    }

    init(record: DailyRecord) {
        modeLabel = "Daily #\(record.puzzleNumber)"
        movesLine = record.movesUsed.map { "\($0)/\(GameMode.dailyMoveBudget) moves" }
        score = record.score
        bestTile = record.bestTile
        cascadeCount = record.cascadeCount
        deepestRound = 0
        board = nil
    }
}

/// The rendered share image: 360×450 logical → 1080×1350 at 3× (feed-friendly
/// 4:5). Built from the same design tokens as the game so it always matches.
struct ShareCardView: View {
    let model: ShareCardModel

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .firstTextBaseline) {
                Text("Gravitile")
                    .font(Theme.display(20))
                    .foregroundStyle(Theme.textPrimary)
                Spacer()
                Text(model.modeLabel.uppercased())
                    .font(.system(size: 12, weight: .heavy))
                    .tracking(1.6)
                    .foregroundStyle(Theme.accent)
            }

            VStack(alignment: .leading, spacing: 2) {
                Text("\(model.score)")
                    .font(Theme.display(42))
                    .foregroundStyle(Theme.accent)
                    .minimumScaleFactor(0.5)
                    .lineLimit(1)
                HStack(spacing: 8) {
                    Text("points")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(Theme.textSecondary)
                    if let movesLine = model.movesLine {
                        Text("· \(movesLine)")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(Theme.textSecondary)
                    }
                }
            }

            HStack(spacing: 10) {
                chip {
                    HStack(spacing: 6) {
                        RoundedRectangle(cornerRadius: 5, style: .continuous)
                            .fill(Theme.tileColor(for: model.bestTile))
                            .frame(width: 20, height: 20)
                        Text("\(model.bestTile)")
                            .font(.system(size: 15, weight: .heavy, design: .rounded))
                            .foregroundStyle(Theme.textPrimary)
                    }
                }
                chip {
                    Text("🌀 \(model.cascadeCount)")
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(Theme.textPrimary)
                }
                if model.deepestRound >= 2 {
                    chip {
                        Text("×\(model.deepestRound) deep")
                            .font(.system(size: 15, weight: .bold))
                            .foregroundStyle(Theme.accent)
                    }
                }
                Spacer()
            }

            if let board = model.board {
                miniBoard(board)
            }

            Spacer(minLength: 0)

            Text("Merge tiles. Gravity turns. — App Store")
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
        }
        .padding(22)
        .frame(width: 360, height: 450, alignment: .topLeading)
        .background(Theme.bgDeep)
    }

    private func chip(@ViewBuilder content: () -> some View) -> some View {
        content()
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(Capsule().fill(Theme.bgBoard))
    }

    private func miniBoard(_ board: Board) -> some View {
        let cell: CGFloat = 38
        let spacing: CGFloat = 4
        return VStack(spacing: spacing) {
            ForEach(0..<Board.size, id: \.self) { row in
                HStack(spacing: spacing) {
                    ForEach(0..<Board.size, id: \.self) { col in
                        let tile = board[Coordinate(row: row, col: col)]
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
                            .fill(tile.map { $0.ice > 0 ? Theme.frost.opacity(0.28) : Theme.tileColor(for: $0.value) } ?? Theme.cellWell)
                            .overlay {
                                if let tile {
                                    Text("\(tile.value)")
                                        .font(.system(size: cell * 0.34, weight: .heavy, design: .rounded))
                                        .minimumScaleFactor(0.4)
                                        .lineLimit(1)
                                        .padding(2)
                                        .foregroundStyle(
                                            tile.ice > 0 ? Theme.frost : Theme.tileTextColor(for: tile.value)
                                        )
                                }
                            }
                            .frame(width: cell, height: cell)
                    }
                }
            }
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(Theme.bgBoard))
        .frame(maxWidth: .infinity, alignment: .center)
    }
}

/// One share tap carries both formats: the emoji text (the viral spine) and
/// the rendered card (the reach) — destinations keep what they support.
struct SharePayload: Identifiable {
    let id = UUID()
    let items: [Any]
}

@MainActor
enum ShareCardRenderer {
    static func payload(for game: GameState) -> SharePayload {
        payload(text: ShareCard.text(for: game), model: ShareCardModel(game: game))
    }

    static func payload(for record: DailyRecord) -> SharePayload {
        let text = ShareCard.text(
            mode: .daily(puzzleNumber: record.puzzleNumber),
            score: record.score, bestTile: record.bestTile,
            cascadeCount: record.cascadeCount, movesUsed: record.movesUsed
        )
        return payload(text: text, model: ShareCardModel(record: record))
    }

    private static func payload(text: String, model: ShareCardModel) -> SharePayload {
        let renderer = ImageRenderer(content: ShareCardView(model: model))
        renderer.scale = 3
        var items: [Any] = [text]
        if let image = renderer.uiImage {
            items.append(image)
        }
        return SharePayload(items: items)
    }
}
