import OrbitKit
import SwiftUI

/// What the world earned while you were gone. Shown once, dismissed by tapping.
struct WelcomeBackPanel: View {
    let summary: WelcomeBack
    let palette: WorldPalette
    var onDismiss: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("While you were away")
                .font(Theme.display(21))
                .foregroundStyle(palette.textPrimary)

            Text("\(Format.duration(summary.seconds)) of production.")
                .font(.system(size: 14))
                .foregroundStyle(palette.textSecondary)

            VStack(alignment: .leading, spacing: 7) {
                ForEach(summary.gained.nonZeroKinds) { kind in
                    HStack(spacing: 8) {
                        Image(systemName: kind.symbol)
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(Theme.color(for: kind))
                            .frame(width: 16)
                        Text(kind.displayName)
                            .font(.system(size: 14))
                            .foregroundStyle(palette.textSecondary)
                        Spacer()
                        Text("+\(Format.quantity(summary.gained[kind]))")
                            .font(Theme.numeral(15, weight: .bold))
                            .foregroundStyle(palette.textPrimary)
                    }
                }
            }

            if summary.meteors > 0 {
                Text("\(summary.meteors) meteors came down unattended — the drones recovered \(Format.quantity(summary.meteorOre)) ore.")
                    .font(.system(size: 13))
                    .foregroundStyle(palette.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if summary.axisMovedDegrees > 2 {
                Text("Your axis drifted \(Format.degrees(summary.axisMovedDegrees)). The climate has moved with it.")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(palette.accent)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Button(action: onDismiss) {
                Text("Back to work")
                    .font(.system(size: 15, weight: .bold))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 13)
                    .background(palette.accent, in: .rect(cornerRadius: 14))
                    .foregroundStyle(palette.space)
            }
            .buttonStyle(.plain)
            .padding(.top, 2)
        }
        .padding(22)
        .frame(maxWidth: 380)
        .background(palette.panel, in: .rect(cornerRadius: 24))
        .padding(.horizontal, 24)
    }
}

/// The prestige moment. It has to feel like a decision, not a button.
struct CollapsePanel: View {
    let store: WorldStore
    var onDismiss: () -> Void

    private var palette: WorldPalette { store.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            Text("Collapse the world")
                .font(Theme.display(23))
                .foregroundStyle(palette.textPrimary)

            Text("Everything on the surface is crushed into the core. What you keep is gravity — and gravity is forever.")
                .font(.system(size: 14))
                .foregroundStyle(palette.textSecondary)
                .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 9) {
                gain("Gravity earned", "+\(store.world.pendingGravity)", palette.accent)
                gain("Next world", "\(World.frequency(forTier: store.world.state.tier + 1) * World.frequency(forTier: store.world.state.tier + 1) * 10 + 2) tiles", palette.textPrimary)
                gain("All yields", "+\(Int((store.world.state.gravity + store.world.pendingGravity) * 2))%", palette.textPrimary)
            }
            .padding(.vertical, 4)

            HStack(spacing: 10) {
                Button {
                    store.collapse()
                    onDismiss()
                } label: {
                    Text("Collapse")
                        .font(.system(size: 15, weight: .bold))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 13)
                        .background(palette.accent, in: .rect(cornerRadius: 14))
                        .foregroundStyle(palette.space)
                }
                .buttonStyle(.plain)

                Button("Not yet", action: onDismiss)
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(palette.textSecondary)
                    .padding(.horizontal, 6)
            }
        }
        .padding(22)
        .frame(maxWidth: 380)
        .background(palette.panel, in: .rect(cornerRadius: 24))
        .padding(.horizontal, 24)
    }

    private func gain(_ title: String, _ value: String, _ color: Color) -> some View {
        HStack {
            Text(title)
                .font(.system(size: 14))
                .foregroundStyle(palette.textSecondary)
            Spacer()
            Text(value)
                .font(Theme.numeral(17, weight: .bold))
                .foregroundStyle(color)
        }
    }
}

/// Four sentences, then out of the way. The mechanic teaches itself once the
/// player sees the pole move.
struct TutorialOverlay: View {
    let palette: WorldPalette
    var onFinish: () -> Void
    @State private var step = 0

    private let steps: [(title: String, body: String)] = [
        ("This is your world",
         "It spins. Drag to look around, pinch to come closer."),
        ("Build where the climate suits",
         "Solar Arrays want sunlight. Condensers want the frost line — the pale ring near each pole."),
        ("Mass drifts to the equator",
         "Everything you build has weight, and weight pulls itself toward the equator. Build heavy on one side and the whole world turns."),
        ("The pale shaft is where it wants to spin",
         "When it separates from the bright one, your world is turning. That gap is Wobble — useful, and past 25° it cracks ground."),
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 13) {
            Text(steps[step].title)
                .font(Theme.display(21))
                .foregroundStyle(palette.textPrimary)
            Text(steps[step].body)
                .font(.system(size: 15))
                .foregroundStyle(palette.textSecondary)
                .fixedSize(horizontal: false, vertical: true)

            HStack(spacing: 6) {
                ForEach(steps.indices, id: \.self) { index in
                    Capsule()
                        .fill(index == step ? palette.accent : palette.panelEdge)
                        .frame(width: index == step ? 18 : 6, height: 6)
                }
                Spacer()
                Button(step == steps.count - 1 ? "Start" : "Next") {
                    if step == steps.count - 1 {
                        onFinish()
                    } else {
                        withAnimation(.easeOut(duration: 0.25)) { step += 1 }
                    }
                }
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(palette.accent)
            }
            .padding(.top, 3)
        }
        .padding(22)
        .frame(maxWidth: 380)
        .background(palette.panel, in: .rect(cornerRadius: 24))
        .padding(.horizontal, 24)
    }
}

/// Floating messages over the world.
struct ToastStack: View {
    let toasts: [Toast]
    let palette: WorldPalette

    var body: some View {
        VStack(spacing: 6) {
            ForEach(toasts) { toast in
                Text(toast.text)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(color(for: toast.tone))
                    .padding(.vertical, 8)
                    .padding(.horizontal, 14)
                    .background(palette.panel.opacity(0.92), in: .capsule)
                    .transition(.move(edge: .top).combined(with: .opacity))
            }
        }
        .animation(.easeOut(duration: 0.22), value: toasts)
    }

    private func color(for tone: Toast.Tone) -> Color {
        switch tone {
        case .neutral: palette.textPrimary
        case .good: palette.accent
        case .warning: palette.warning
        }
    }
}
