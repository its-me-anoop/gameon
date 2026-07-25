import OrbitKit
import RealityKit
import SwiftUI

/// The world itself: a RealityKit scene under a thin layer of gestures.
///
/// Drag orbits, pinch zooms, tap places or inspects. The planet keeps turning
/// under your finger, because it is a world and not a menu.
struct PlanetView: View {
    let store: WorldStore

    @State private var scene = PlanetScene()
    @State private var dragAnchor: CameraRig?
    @State private var zoomAnchor: Double?
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        GeometryReader { proxy in
            let size = proxy.size

            ZStack {
                RealityView { content in
                    scene.attach(to: &content, world: store.world, palette: store.palette)
                } update: { _ in
                    scene.update(
                        world: store.world, palette: store.palette,
                        selection: store.selectedTile, rig: store.camera
                    )
                }
                .gesture(orbit(in: size))
                .simultaneousGesture(zoom)
                .simultaneousGesture(tap(in: size))

                meteorTarget(in: size)
            }
            .accessibilityElement()
            .accessibilityLabel("The world")
            .accessibilityValue(accessibilitySummary)
            .accessibilityHint("Double tap a machine in the tray, then double tap here to place it")
        }
    }

    // MARK: - Gestures

    private func orbit(in size: CGSize) -> some Gesture {
        DragGesture(minimumDistance: 6)
            .onChanged { value in
                let anchor = dragAnchor ?? store.camera
                if dragAnchor == nil { dragAnchor = anchor }
                var rig = anchor
                rig.orbit(
                    byX: Double(value.translation.width),
                    y: Double(value.translation.height),
                    viewHeight: Double(size.height)
                )
                store.camera = rig
            }
            .onEnded { _ in dragAnchor = nil }
    }

    private var zoom: some Gesture {
        MagnifyGesture()
            .onChanged { value in
                let anchor = zoomAnchor ?? store.camera.distance
                if zoomAnchor == nil { zoomAnchor = anchor }
                var rig = store.camera
                rig.distance = anchor
                rig.zoom(by: value.magnification)
                store.camera = rig
            }
            .onEnded { _ in zoomAnchor = nil }
    }

    private func tap(in size: CGSize) -> some Gesture {
        SpatialTapGesture()
            .onEnded { value in
                guard let tile = scene.tile(
                    at: value.location, viewSize: size, rig: store.camera, world: store.world
                ) else {
                    store.selectedTile = nil
                    return
                }
                store.tapped(tile: tile)
            }
    }

    // MARK: - Meteor

    /// A generous tap target laid over the meteor, positioned with the same
    /// projection the renderer used.
    @ViewBuilder
    private func meteorTarget(in size: CGSize) -> some View {
        if let position = PlanetScene.meteorPosition(world: store.world),
           let point = store.camera.project(position, in: size) {
            Button {
                store.catchMeteor()
            } label: {
                Circle()
                    .strokeBorder(store.palette.warning, lineWidth: 2)
                    .frame(width: 74, height: 74)
                    .background(Circle().fill(store.palette.warning.opacity(0.12)))
                    .overlay(alignment: .bottom) {
                        Text("CATCH")
                            .font(.system(size: 11, weight: .heavy, design: .rounded))
                            .tracking(1.4)
                            .foregroundStyle(store.palette.warning)
                            .offset(y: 20)
                    }
            }
            .buttonStyle(.plain)
            .position(point)
            .scaleEffect(reduceMotion ? 1 : pulse)
            .animation(
                reduceMotion ? nil : .easeOut(duration: 0.9).repeatForever(autoreverses: true),
                value: pulse
            )
            .allowsHitTesting(true)
            .transition(.opacity)
        }
    }

    private var pulse: CGFloat { store.world.state.activeMeteor == nil ? 1 : 1.12 }

    private var accessibilitySummary: String {
        let world = store.world
        let wobble = Int(world.wobbleDegrees.rounded())
        return "\(world.state.buildings.count) machines, mass \(Int(world.totalMass)), wobble \(wobble) degrees"
    }
}
