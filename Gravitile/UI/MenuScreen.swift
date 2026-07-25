import OrbitKit
import SwiftUI

/// Records, settings and the leaderboards, in one sheet. A game this small
/// doesn't need a menu tree.
struct MenuScreen: View {
    let store: WorldStore
    @Environment(\.dismiss) private var dismiss
    @State private var showsGameCenter = false
    @State private var confirmingReset = false

    private var palette: WorldPalette { store.palette }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 28) {
                    records
                    themes
                    toggles
                    about
                }
                .padding(20)
            }
            .background(palette.space)
            .navigationTitle("Gravitile")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                        .foregroundStyle(palette.accent)
                }
            }
        }
        .tint(palette.accent)
        .sheet(isPresented: $showsGameCenter) {
            GameCenterView()
                .ignoresSafeArea()
        }
    }

    // MARK: Sections

    private var records: some View {
        section("Records") {
            VStack(spacing: 0) {
                row("Heaviest world", Format.quantity(max(store.records.heaviestWorld, store.world.totalMass)))
                row("Total gravity", "\(store.world.state.lifetimeGravity)")
                row("Collapses", "\(store.world.state.collapses)")
                row("Deepest world", "\(store.world.geometry.tileCount) tiles")
                row("Machines built", "\(store.world.state.placements)")
                row("Meteors caught", "\(store.world.state.meteorsCaught)")
                if let fastest = store.records.fastestFirstCollapse {
                    row("Fastest first collapse", Format.duration(fastest))
                }
            }

            Button {
                showsGameCenter = true
            } label: {
                Text("Leaderboards & achievements")
                    .font(.system(size: 15, weight: .semibold))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 13)
                    .background(palette.panel, in: .rect(cornerRadius: 14))
                    .foregroundStyle(palette.accent)
            }
            .buttonStyle(.plain)
            .padding(.top, 10)
        }
    }

    private var themes: some View {
        section("Color world") {
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 10) {
                    ForEach(Theme.palettes) { option in
                        Button {
                            store.settings.themeID = option.id
                            store.sound.tap()
                        } label: {
                            VStack(alignment: .leading, spacing: 8) {
                                HStack(spacing: 4) {
                                    ForEach(Biome.allCases, id: \.self) { biome in
                                        RoundedRectangle(cornerRadius: 3)
                                            .fill(option.color(for: biome))
                                            .frame(width: 15, height: 26)
                                    }
                                }
                                Text(option.name)
                                    .font(.system(size: 14, weight: .bold))
                                    .foregroundStyle(palette.textPrimary)
                                Text(option.tagline)
                                    .font(.system(size: 11))
                                    .foregroundStyle(palette.textSecondary)
                                    .lineLimit(1)
                            }
                            .padding(12)
                            .frame(width: 176, alignment: .leading)
                            .background(palette.panel, in: .rect(cornerRadius: 16))
                            .overlay {
                                RoundedRectangle(cornerRadius: 16)
                                    .strokeBorder(
                                        store.settings.themeID == option.id ? palette.accent : .clear,
                                        lineWidth: 2
                                    )
                            }
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            .scrollClipDisabled()
        }
    }

    private var toggles: some View {
        section("Sound & feel") {
            VStack(spacing: 0) {
                toggle("Sound effects", isOn: Binding(
                    get: { store.settings.soundOn }, set: { store.settings.soundOn = $0 }
                ))
                toggle("Ambient music", isOn: Binding(
                    get: { store.settings.musicOn }, set: { store.settings.musicOn = $0 }
                ))
                toggle("Haptics", isOn: Binding(
                    get: { store.settings.hapticsOn }, set: { store.settings.hapticsOn = $0 }
                ))
            }
        }
    }

    private var about: some View {
        section("About") {
            Text("Every part of this game is made in its own repository: the tiling, the physics that steers your axis, the sounds and the sky are all generated by our own tools. No ads, no tracking, no account.")
                .font(.system(size: 13))
                .foregroundStyle(palette.textSecondary)
                .fixedSize(horizontal: false, vertical: true)

            Button(role: .destructive) {
                confirmingReset = true
            } label: {
                Text("Start a brand new world")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(palette.warning)
            }
            .padding(.top, 8)
            .confirmationDialog(
                "Erase this world and everything in it?",
                isPresented: $confirmingReset, titleVisibility: .visible
            ) {
                Button("Erase and start over", role: .destructive) {
                    store.reset()
                    dismiss()
                }
                Button("Keep playing", role: .cancel) {}
            }
        }
    }

    // MARK: Building blocks

    private func section(
        _ title: String, @ViewBuilder content: () -> some View
    ) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(title.uppercased())
                .font(.system(size: 11, weight: .bold, design: .rounded))
                .tracking(1.4)
                .foregroundStyle(palette.textSecondary)
            content()
        }
    }

    private func row(_ title: String, _ value: String) -> some View {
        HStack {
            Text(title)
                .font(.system(size: 15))
                .foregroundStyle(palette.textSecondary)
            Spacer()
            Text(value)
                .font(Theme.numeral(16, weight: .bold))
                .foregroundStyle(palette.textPrimary)
        }
        .padding(.vertical, 9)
        .overlay(alignment: .bottom) {
            Rectangle()
                .fill(palette.panelEdge.opacity(0.5))
                .frame(height: 1)
        }
    }

    private func toggle(_ title: String, isOn: Binding<Bool>) -> some View {
        Toggle(isOn: isOn) {
            Text(title)
                .font(.system(size: 15))
                .foregroundStyle(palette.textPrimary)
        }
        .tint(palette.accent)
        .padding(.vertical, 7)
    }
}
