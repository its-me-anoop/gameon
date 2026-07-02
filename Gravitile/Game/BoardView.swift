import SwiftUI
import GravitileKit

struct BoardView: View {
    let viewModel: GameViewModel
    var onSwipe: (Direction) -> Void

    private let spacing: CGFloat = 8

    var body: some View {
        GeometryReader { proxy in
            let side = min(proxy.size.width, proxy.size.height)
            let cell = (side - spacing * CGFloat(Board.size + 1)) / CGFloat(Board.size)

            ZStack {
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .fill(Theme.bgBoard)
                    .frame(width: side, height: side)

                // Cell wells
                ForEach(0..<Board.size * Board.size, id: \.self) { index in
                    let coordinate = Coordinate(row: index / Board.size, col: index % Board.size)
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(Theme.cellWell)
                        .frame(width: cell, height: cell)
                        .position(center(of: coordinate, cell: cell, side: side))
                }

                // Tiles
                ForEach(viewModel.tiles) { tile in
                    TileView(value: tile.value, size: cell)
                        .scaleEffect(tile.scale)
                        .opacity(tile.opacity)
                        .position(center(of: tile.coordinate, cell: cell, side: side))
                        .zIndex(Double(tile.value))
                }

                // Cascade particle bursts
                ForEach(Array(viewModel.burstCells.enumerated()), id: \.offset) { _, burst in
                    if burst.1 >= 1 {
                        ParticleBurstView(round: burst.1)
                            .frame(width: cell * 2, height: cell * 2)
                            .position(center(of: burst.0, cell: cell, side: side))
                            .allowsHitTesting(false)
                    }
                }
            }
            .frame(width: proxy.size.width, height: proxy.size.height)
            .contentShape(Rectangle())
            .gesture(swipeGesture)
        }
        .aspectRatio(1, contentMode: .fit)
        .accessibilityElement(children: .ignore)
        .accessibilityIdentifier("board")
        .accessibilityLabel("Game board")
        .accessibilityValue(accessibilitySummary)
        .accessibilityActions {
            Button("Swipe up") { onSwipe(.up) }
            Button("Swipe down") { onSwipe(.down) }
            Button("Swipe left") { onSwipe(.left) }
            Button("Swipe right") { onSwipe(.right) }
        }
    }

    private func center(of coordinate: Coordinate, cell: CGFloat, side: CGFloat) -> CGPoint {
        let origin = (side - CGFloat(Board.size) * cell - CGFloat(Board.size - 1) * spacing) / 2
        return CGPoint(
            x: origin + CGFloat(coordinate.col) * (cell + spacing) + cell / 2,
            y: origin + CGFloat(coordinate.row) * (cell + spacing) + cell / 2
        )
    }

    private var swipeGesture: some Gesture {
        DragGesture(minimumDistance: 24)
            .onEnded { gesture in
                let dx = gesture.translation.width
                let dy = gesture.translation.height
                let direction: Direction = abs(dx) > abs(dy)
                    ? (dx > 0 ? .right : .left)
                    : (dy > 0 ? .down : .up)
                onSwipe(direction)
            }
    }

    private var accessibilitySummary: String {
        (0..<Board.size).map { row in
            let cells = (0..<Board.size).map { col in
                viewModel.game.board[Coordinate(row: row, col: col)].map { String($0.value) } ?? "empty"
            }
            return "Row \(row + 1): " + cells.joined(separator: ", ")
        }.joined(separator: ". ")
    }
}

struct TileView: View {
    let value: Int
    let size: CGFloat

    var body: some View {
        RoundedRectangle(cornerRadius: 10, style: .continuous)
            .fill(Theme.tileColor(for: value))
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(.white.opacity(0.12), lineWidth: 1)
            )
            .overlay(
                Text("\(value)")
                    .font(Theme.tileNumeral(numeralSize))
                    .minimumScaleFactor(0.5)
                    .lineLimit(1)
                    .padding(4)
                    .foregroundStyle(Theme.tileTextColor(for: value))
            )
            .frame(width: size, height: size)
    }

    private var numeralSize: CGFloat {
        switch value {
        case ..<100: size * 0.44
        case ..<1000: size * 0.36
        case ..<10000: size * 0.3
        default: size * 0.26
        }
    }
}
