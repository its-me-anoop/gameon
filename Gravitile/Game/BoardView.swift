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
                    TileView(value: tile.value, size: cell, ice: tile.ice, isMath: isMath)
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

                // Floating equations over popped bonds (Math Pop)
                ForEach(viewModel.equationPops) { pop in
                    EquationPopView(text: pop.text)
                        .position(center(of: pop.coordinate, cell: cell, side: side))
                        .allowsHitTesting(false)
                        .zIndex(100_000)
                }
            }
            .offset(viewModel.boardNudge)
            .modifier(ShakeEffect(travel: viewModel.shakeTravel, magnitude: viewModel.shakeMagnitude))
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

    private var isMath: Bool {
        if case .math = viewModel.game.mode { return true }
        return false
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
    /// Boulder ice HP; 0 renders a normal tile, 1 a cracked shell, 2 intact.
    var ice: Int = 0
    /// Math Pop tiles use the Cuisenaire digit colors instead of the ramp.
    var isMath: Bool = false

    var body: some View {
        RoundedRectangle(cornerRadius: 10, style: .continuous)
            .fill(ice > 0 ? Theme.frost.opacity(0.28) : fillColor)
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(
                        ice > 0 ? Theme.frost.opacity(0.9) : .white.opacity(0.12),
                        style: StrokeStyle(lineWidth: ice > 0 ? 2 : 1, dash: ice == 1 ? [5, 3] : [])
                    )
            )
            .overlay(
                Text("\(value)")
                    .font(Theme.tileNumeral(numeralSize))
                    .minimumScaleFactor(0.5)
                    .lineLimit(1)
                    .padding(4)
                    .foregroundStyle(ice > 0 ? Theme.frost : textColor)
            )
            .overlay(alignment: .topTrailing) {
                if ice > 0 {
                    Image(systemName: "snowflake")
                        .font(.system(size: size * 0.18, weight: .bold))
                        .foregroundStyle(Theme.frost)
                        .padding(size * 0.07)
                        .opacity(ice >= 2 ? 1 : 0.55)
                }
            }
            .frame(width: size, height: size)
            .accessibilityLabel(ice > 0 ? "Iced tile \(value), \(ice) hits to free" : "Tile \(value)")
    }

    private var fillColor: Color {
        isMath ? Theme.mathTileColor(for: value) : Theme.tileColor(for: value)
    }

    private var textColor: Color {
        isMath ? Theme.mathTileTextColor(for: value) : Theme.tileTextColor(for: value)
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

/// Rises and fades from a popped bond, like ScorePopView but board-anchored.
struct EquationPopView: View {
    let text: String
    @State private var risen = false

    var body: some View {
        Text(text)
            .font(Theme.display(15, weight: .bold))
            .foregroundStyle(Theme.textPrimary)
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Capsule().fill(Theme.bgBoard.opacity(0.92)))
            .offset(y: risen ? -46 : -10)
            .opacity(risen ? 0 : 1)
            .onAppear { withAnimation(.easeOut(duration: 1.1)) { risen = true } }
    }
}
