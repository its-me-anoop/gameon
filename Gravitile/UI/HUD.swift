import OrbitKit
import SwiftUI

/// The top strip: what the world holds, what it earns, and how far it is from
/// where it wants to spin.
struct HUDBar: View {
    let store: WorldStore
    var onMenu: () -> Void

    private var palette: WorldPalette { store.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .top) {
                resources
                Spacer(minLength: 12)
                WobbleGauge(
                    degrees: store.world.wobbleDegrees,
                    threshold: World.quakeWobbleThreshold,
                    palette: palette
                )
            }

            HStack(spacing: 10) {
                Button(action: onMenu) {
                    Label("Menu", systemImage: "line.3.horizontal")
                        .labelStyle(.iconOnly)
                        .font(.system(size: 17, weight: .semibold))
                        .frame(width: 38, height: 38)
                        .background(palette.panel.opacity(0.85), in: .circle)
                        .foregroundStyle(palette.textSecondary)
                }
                .accessibilityLabel("Menu")

                massReadout
                Spacer(minLength: 0)
            }
        }
    }

    private var resources: some View {
        HStack(spacing: 0) {
            ForEach(visibleResources, id: \.self) { kind in
                ResourceReadout(
                    kind: kind,
                    amount: store.world.state.resources[kind],
                    rate: store.netRates[kind],
                    cap: store.world.storageCaps[kind],
                    palette: palette
                )
                if kind != visibleResources.last {
                    Rectangle()
                        .fill(palette.panelEdge.opacity(0.5))
                        .frame(width: 1, height: 22)
                        .padding(.horizontal, 7)
                }
            }
        }
        .padding(.vertical, 9)
        .padding(.horizontal, 12)
        .background(palette.panel.opacity(0.88), in: .rect(cornerRadius: 16))
    }

    /// Refined resources stay hidden until the world can actually make them —
    /// an empty row of zeroes teaches nothing.
    private var visibleResources: [ResourceKind] {
        ResourceKind.allCases.filter { kind in
            switch kind {
            case .ore, .charge: true
            case .water: store.world.state.resources.water > 0 || store.records.collapses > 0
            case .alloy: store.world.state.resources.alloy > 0 || store.records.collapses > 0
            case .biomass: store.world.isUnlocked(.greenhouse)
            }
        }
    }

    private var massReadout: some View {
        HStack(spacing: 7) {
            Text("MASS")
                .font(.system(size: 10, weight: .bold, design: .rounded))
                .tracking(1.3)
                .foregroundStyle(palette.textSecondary)
            Text(Format.quantity(store.world.totalMass))
                .font(Theme.numeral(17, weight: .bold))
                .foregroundStyle(palette.textPrimary)
                .contentTransition(.numericText())
            Text("/ \(Format.quantity(store.world.collapseThreshold))")
                .font(Theme.numeral(13))
                .foregroundStyle(palette.textSecondary)
            if store.world.state.gravity > 0 {
                Text("· G\(store.world.state.gravity)")
                    .font(Theme.numeral(13, weight: .bold))
                    .foregroundStyle(palette.accent)
            }
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 13)
        .background(palette.panel.opacity(0.85), in: .capsule)
    }
}

private struct ResourceReadout: View {
    let kind: ResourceKind
    let amount: Double
    let rate: Double
    let cap: Double
    let palette: WorldPalette

    private var isFull: Bool { amount >= cap - 0.5 }

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            HStack(spacing: 4) {
                Image(systemName: kind.symbol)
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(Theme.color(for: kind))
                Text(Format.quantity(amount))
                    .font(Theme.numeral(14, weight: .bold))
                    .foregroundStyle(isFull ? palette.warning : palette.textPrimary)
                    .contentTransition(.numericText())
            }
            Text(isFull ? "full" : Format.rate(rate))
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(palette.textSecondary)
        }
        .frame(minWidth: 42, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(kind.displayName) \(Format.quantity(amount)), \(Format.rate(rate))")
    }
}

/// The signature readout: how far the world is from where its mass wants it to
/// spin. Calm at rest, hot when you are steering hard enough to crack ground.
struct WobbleGauge: View {
    let degrees: Double
    let threshold: Double
    let palette: WorldPalette

    private var fraction: Double { min(degrees / 90, 1) }
    private var isHot: Bool { degrees > threshold }

    var body: some View {
        VStack(spacing: 2) {
            ZStack {
                Circle()
                    .trim(from: 0.12, to: 0.88)
                    .stroke(palette.panelEdge, style: .init(lineWidth: 4, lineCap: .round))
                Circle()
                    .trim(from: 0.12, to: 0.12 + 0.76 * fraction)
                    .stroke(
                        isHot ? palette.warning : palette.accent,
                        style: .init(lineWidth: 4, lineCap: .round)
                    )
                Text(Format.degrees(degrees))
                    .font(Theme.numeral(14, weight: .bold))
                    .foregroundStyle(isHot ? palette.warning : palette.textPrimary)
                    .contentTransition(.numericText())
            }
            .rotationEffect(.degrees(90))
            .frame(width: 52, height: 52)

            Text("WOBBLE")
                .font(.system(size: 9, weight: .bold, design: .rounded))
                .tracking(1.2)
                .foregroundStyle(palette.textSecondary)
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 10)
        .background(palette.panel.opacity(0.88), in: .rect(cornerRadius: 16))
        .animation(.easeOut(duration: 0.4), value: fraction)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Wobble \(Format.degrees(degrees))")
        .accessibilityHint(isHot ? "High enough to crack ground" : "Settled")
    }
}
