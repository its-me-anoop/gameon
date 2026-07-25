import OrbitKit
import SwiftUI

/// What a piece of ground is worth, and what you can do about it.
struct TileInspector: View {
    let store: WorldStore
    let readout: TileReadout

    private var palette: WorldPalette { store.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 11) {
            header

            HStack(spacing: 18) {
                stat("Temperature", Format.temperature(store.world.climate.celsius(at: center)))
                stat("Sunlight", "\(Int((readout.solarFactor * 100).rounded()))%")
                if readout.terrain.oreRichness > 1.2 {
                    stat("Crust", "Rich")
                } else if readout.terrain.hasIce {
                    stat("Crust", "Icy")
                }
            }

            if let building = readout.building {
                machine(building)
            } else {
                Text(suggestion)
                    .font(.system(size: 13))
                    .foregroundStyle(palette.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(16)
        .background(palette.panel.opacity(0.94), in: .rect(cornerRadius: 20))
        .overlay {
            RoundedRectangle(cornerRadius: 20)
                .strokeBorder(palette.panelEdge.opacity(0.7), lineWidth: 1)
        }
        .padding(.horizontal, 16)
        .transition(.move(edge: .bottom).combined(with: .opacity))
    }

    private var center: Vec3 { store.world.geometry.tiles[readout.tile].center }

    private var header: some View {
        HStack(alignment: .firstTextBaseline) {
            Text(readout.biome.displayName)
                .font(Theme.display(19))
                .foregroundStyle(palette.textPrimary)
            if readout.isCracked {
                Text("CRACKED")
                    .font(.system(size: 10, weight: .heavy, design: .rounded))
                    .tracking(1.2)
                    .foregroundStyle(palette.warning)
            }
            Spacer()
            Button {
                store.selectedTile = nil
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(palette.textSecondary)
                    .frame(width: 30, height: 30)
            }
            .accessibilityLabel("Close")
        }
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title.uppercased())
                .font(.system(size: 9, weight: .bold, design: .rounded))
                .tracking(1.1)
                .foregroundStyle(palette.textSecondary)
            Text(value)
                .font(Theme.numeral(16, weight: .bold))
                .foregroundStyle(palette.textPrimary)
        }
    }

    private func machine(_ building: Building) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Circle().fill(palette.color(for: building.kind)).frame(width: 10, height: 10)
                Text("\(building.kind.displayName) · Level \(building.level)")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(palette.textPrimary)
                Spacer()
            }

            if let output = building.kind.baseOutput {
                HStack(spacing: 6) {
                    Text(Format.rate(readout.output[output.kind]))
                        .font(Theme.numeral(15, weight: .bold))
                        .foregroundStyle(Theme.color(for: output.kind))
                    Text(output.kind.displayName)
                        .font(.system(size: 13))
                        .foregroundStyle(palette.textSecondary)
                    if readout.yieldMultiplier < 0.35 {
                        Text("· wrong climate for it")
                            .font(.system(size: 12))
                            .foregroundStyle(palette.warning)
                    }
                }
            } else if building.kind == .ballast {
                Text("Produces nothing. Its weight is the point.")
                    .font(.system(size: 13))
                    .foregroundStyle(palette.textSecondary)
            }

            HStack(spacing: 9) {
                Button {
                    store.upgradeSelected()
                } label: {
                    Text("Upgrade  \(costLine(building.upgradeCost))")
                        .font(.system(size: 13, weight: .bold))
                        .padding(.vertical, 10)
                        .frame(maxWidth: .infinity)
                        .background(
                            store.world.canUpgrade(tile: readout.tile)
                                ? palette.accent.opacity(0.9) : palette.panelEdge,
                            in: .rect(cornerRadius: 12)
                        )
                        .foregroundStyle(
                            store.world.canUpgrade(tile: readout.tile)
                                ? palette.space : palette.textSecondary
                        )
                }
                .buttonStyle(.plain)

                Button {
                    store.demolishSelected()
                } label: {
                    Text("Remove")
                        .font(.system(size: 13, weight: .semibold))
                        .padding(.vertical, 10)
                        .padding(.horizontal, 14)
                        .background(palette.panelEdge.opacity(0.7), in: .rect(cornerRadius: 12))
                        .foregroundStyle(palette.textSecondary)
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func costLine(_ cost: Resources) -> String {
        cost.nonZeroKinds
            .map { "\(Format.quantity(cost[$0])) \($0.displayName.lowercased())" }
            .joined(separator: " · ")
    }

    /// Tells the player what this ground is good for, which is more useful than
    /// telling them what it is.
    private var suggestion: String {
        switch readout.biome {
        case .frozen: "Too cold for most machines. Condensers love the edge of it."
        case .tundra: "Cold ground — the frost line is near, so Condensers do well."
        case .temperate: "Mild. Greenhouses want exactly this."
        case .arid: "Warm and bright. Solar Arrays and Smelters both work here."
        case .molten: "Fierce heat. Smelters run at full tilt; nothing living would."
        }
    }
}
