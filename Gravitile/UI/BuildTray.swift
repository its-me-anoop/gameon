import OrbitKit
import SwiftUI

/// The machines you can pick up. Choosing one arms it; the next tap on the
/// world puts it down.
struct BuildTray: View {
    let store: WorldStore

    private var palette: WorldPalette { store.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            if let kind = store.armedKind {
                armedBanner(kind)
            }
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 9) {
                    ForEach(store.availableKinds) { kind in
                        MachineChip(
                            kind: kind,
                            cost: store.world.placementCost(kind),
                            affordable: store.world.state.resources.covers(store.world.placementCost(kind)),
                            isArmed: store.armedKind == kind,
                            palette: palette
                        ) {
                            store.armedKind = store.armedKind == kind ? nil : kind
                            store.selectedTile = nil
                            store.sound.tap()
                        }
                    }
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 2)
            }
            .scrollClipDisabled()
        }
    }

    private func armedBanner(_ kind: BuildingKind) -> some View {
        HStack(spacing: 8) {
            Text("Tap the world to place")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(palette.textPrimary)
            Text(kind.displayName)
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(palette.color(for: kind))
            Spacer(minLength: 8)
            Button("Cancel") {
                store.armedKind = nil
                store.sound.tap()
            }
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(palette.textSecondary)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 9)
        .background(palette.panel.opacity(0.9), in: .rect(cornerRadius: 14))
        .padding(.horizontal, 16)
        .transition(.move(edge: .bottom).combined(with: .opacity))
    }
}

private struct MachineChip: View {
    let kind: BuildingKind
    let cost: Resources
    let affordable: Bool
    let isArmed: Bool
    let palette: WorldPalette
    var action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 5) {
                HStack(spacing: 6) {
                    Circle()
                        .fill(palette.color(for: kind))
                        .frame(width: 9, height: 9)
                    Text(kind.displayName)
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(palette.textPrimary)
                        .lineLimit(1)
                }
                HStack(spacing: 8) {
                    ForEach(cost.nonZeroKinds) { resource in
                        HStack(spacing: 3) {
                            Image(systemName: resource.symbol)
                                .font(.system(size: 8, weight: .bold))
                            Text(Format.quantity(cost[resource]))
                                .font(Theme.numeral(11, weight: .semibold))
                        }
                        .foregroundStyle(affordable ? Theme.color(for: resource) : palette.textSecondary)
                    }
                    Text("· \(Format.quantity(kind.baseMass)) mass")
                        .font(.system(size: 10, weight: .medium, design: .rounded))
                        .foregroundStyle(palette.textSecondary)
                }
            }
            .padding(.vertical, 9)
            .padding(.horizontal, 12)
            .frame(minWidth: 132, alignment: .leading)
            .background(
                isArmed ? palette.accent.opacity(0.22) : palette.panel.opacity(0.88),
                in: .rect(cornerRadius: 14)
            )
            .overlay {
                RoundedRectangle(cornerRadius: 14)
                    .strokeBorder(isArmed ? palette.accent : .clear, lineWidth: 1.5)
            }
            .opacity(affordable ? 1 : 0.55)
        }
        .buttonStyle(.plain)
        .accessibilityLabel(kind.displayName)
        .accessibilityValue(affordable ? "Affordable" : "Too expensive")
        .accessibilityHint(kind.tagline)
    }
}
