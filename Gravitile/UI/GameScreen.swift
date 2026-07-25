import OrbitKit
import SwiftUI

/// The whole game. The world fills the screen; everything else stays at the
/// edges and gets out of the way.
struct GameScreen: View {
    @State private var store = WorldStore()
    @State private var showsMenu = false
    @Environment(\.scenePhase) private var scenePhase

    private var palette: WorldPalette { store.palette }

    var body: some View {
        ZStack {
            palette.space.ignoresSafeArea()

            PlanetView(store: store)
                .ignoresSafeArea()

            VStack(spacing: 0) {
                HUDBar(store: store) { showsMenu = true }
                    .padding(.horizontal, 16)
                    .padding(.top, 4)

                ToastStack(toasts: store.toasts, palette: palette)
                    .padding(.top, 10)

                Spacer(minLength: 0)

                bottom
            }

            overlay
        }
        .preferredColorScheme(palette.isLight ? .light : .dark)
        .animation(.easeOut(duration: 0.25), value: store.selectedTile)
        .animation(.easeOut(duration: 0.25), value: store.armedKind)
        .onAppear { store.start() }
        .onChange(of: scenePhase) { _, phase in
            switch phase {
            case .active: store.start()
            case .background, .inactive: store.stop()
            @unknown default: break
            }
        }
        .sheet(isPresented: $showsMenu) {
            MenuScreen(store: store)
                .presentationDetents([.large])
        }
    }

    // MARK: - Bottom stack

    @ViewBuilder
    private var bottom: some View {
        VStack(spacing: 10) {
            if store.world.canCollapse, !store.isShowingCollapse {
                collapseButton
            }
            if let readout = store.selectedReadout, store.armedKind == nil {
                TileInspector(store: store, readout: readout)
            }
            BuildTray(store: store)
        }
        .padding(.bottom, 10)
    }

    private var collapseButton: some View {
        Button {
            store.isShowingCollapse = true
            store.sound.tap()
        } label: {
            HStack(spacing: 9) {
                Image(systemName: "arrow.down.right.and.arrow.up.left")
                    .font(.system(size: 14, weight: .bold))
                Text("Collapse for +\(store.world.pendingGravity) gravity")
                    .font(.system(size: 15, weight: .bold))
            }
            .padding(.vertical, 12)
            .padding(.horizontal, 20)
            .background(palette.accent, in: .capsule)
            .foregroundStyle(palette.space)
        }
        .buttonStyle(.plain)
        .transition(.scale(scale: 0.9).combined(with: .opacity))
    }

    // MARK: - Modal overlays

    @ViewBuilder
    private var overlay: some View {
        if let summary = store.welcomeBack {
            scrim {
                WelcomeBackPanel(summary: summary, palette: palette) {
                    store.welcomeBack = nil
                }
            }
        } else if store.isShowingCollapse {
            scrim {
                CollapsePanel(store: store) { store.isShowingCollapse = false }
            }
        } else if !store.settings.hasSeenTutorial {
            scrim {
                TutorialOverlay(palette: palette) {
                    store.settings.hasSeenTutorial = true
                }
            }
        }
    }

    private func scrim(@ViewBuilder content: () -> some View) -> some View {
        ZStack {
            palette.space.opacity(0.72)
                .ignoresSafeArea()
            content()
        }
        .transition(.opacity)
    }
}
