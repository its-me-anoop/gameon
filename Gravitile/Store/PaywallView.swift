import SwiftUI
import StoreKit

struct PaywallView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss
    @State private var purchasing = false

    var body: some View {
        ZStack {
            Theme.bgDeep.ignoresSafeArea()

            VStack(alignment: .leading, spacing: 0) {
                Text("Gravitile Plus")
                    .font(Theme.display(30))
                    .foregroundStyle(Theme.textPrimary)
                    .padding(.top, 20)

                Text("One purchase. Yours forever.")
                    .font(.system(size: 15))
                    .foregroundStyle(Theme.textSecondary)
                    .padding(.top, 6)

                VStack(alignment: .leading, spacing: 18) {
                    feature(icon: "calendar.badge.clock", title: "Daily archive",
                            detail: "Replay any past daily puzzle you missed.")
                    feature(icon: "arrow.uturn.backward.circle", title: "Unlimited undo",
                            detail: "Experiment freely — take back any move.")
                    feature(icon: "flame", title: "Protect your streak",
                            detail: "Catch up on a missed day from the archive.")
                    feature(icon: "heart", title: "Support one indie dev",
                            detail: "No ads, no tracking — Plus keeps it that way.")
                }
                .padding(.top, 32)

                Spacer()

                if appModel.store.isPlus {
                    Label("You have Plus — thank you!", systemImage: "checkmark.seal.fill")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(Theme.accent)
                        .frame(maxWidth: .infinity)
                        .padding(.bottom, 24)
                } else {
                    VStack(spacing: 12) {
                        Button {
                            purchase()
                        } label: {
                            HStack {
                                Text("Unlock Plus")
                                Spacer()
                                Text(appModel.store.plusProduct?.displayPrice ?? "—")
                            }
                            .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(PrimaryButtonStyle())
                        .disabled(purchasing || appModel.store.plusProduct == nil)
                        .accessibilityIdentifier("buyPlus")

                        Button("Restore Purchases") {
                            Task { await appModel.store.restore() }
                        }
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(Theme.textSecondary)
                    }
                    .padding(.bottom, 16)
                }
            }
            .padding(.horizontal, 28)
        }
        .navigationTitle("")
        .navigationBarTitleDisplayMode(.inline)
    }

    private func feature(icon: String, title: String, detail: String) -> some View {
        HStack(alignment: .top, spacing: 14) {
            Image(systemName: icon)
                .font(.system(size: 20, weight: .semibold))
                .foregroundStyle(Theme.accent)
                .frame(width: 30)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(Theme.textPrimary)
                Text(detail)
                    .font(.system(size: 14))
                    .foregroundStyle(Theme.textSecondary)
            }
        }
    }

    private func purchase() {
        guard let product = appModel.store.plusProduct else { return }
        purchasing = true
        Task {
            await appModel.store.purchase(product)
            purchasing = false
        }
    }
}
