import SwiftUI
import StoreKit

struct SettingsScreen: View {
    @Environment(AppModel.self) private var appModel
    @State private var showThanks = false

    var body: some View {
        List {
            Section("Game") {
                Toggle("Sound effects", isOn: binding(\.soundOn))
                Toggle("Haptics", isOn: binding(\.hapticsOn))
            }
            .listRowBackground(Theme.bgBoard)

            Section("Gravitile Plus") {
                NavigationLink(value: Route.paywall) {
                    HStack {
                        Label(
                            appModel.store.isPlus ? "Plus unlocked" : "Unlock Plus",
                            systemImage: appModel.store.isPlus ? "checkmark.seal.fill" : "sparkles"
                        )
                        Spacer()
                        if !appModel.store.isPlus, let price = appModel.store.plusProduct?.displayPrice {
                            Text(price).foregroundStyle(Theme.textSecondary)
                        }
                    }
                }
                Button("Restore purchases") {
                    Task { await appModel.store.restore() }
                }
            }
            .listRowBackground(Theme.bgBoard)

            Section {
                ForEach(appModel.store.tipProducts, id: \.id) { product in
                    Button {
                        Task {
                            if await appModel.store.purchase(product) {
                                showThanks = true
                            }
                        }
                    } label: {
                        HStack {
                            Text(product.displayName)
                            Spacer()
                            Text(product.displayPrice).foregroundStyle(Theme.textSecondary)
                        }
                    }
                }
            } header: {
                Text("Tip jar")
            } footer: {
                Text("Gravitile has no ads and collects no data. Tips keep it that way.")
            }
            .listRowBackground(Theme.bgBoard)

            Section("About") {
                LabeledContent("Version", value: appVersion)
                Link("Privacy policy", destination: URL(string: "https://github.com/its-me-anoop/gravitile-support/blob/main/privacy.md")!)
                LabeledContent("Font", value: "Unbounded (OFL)")
            }
            .listRowBackground(Theme.bgBoard)
        }
        .scrollContentBackground(.hidden)
        .background(Theme.bgDeep)
        .navigationTitle("Settings")
        .task {
            await appModel.store.ensureProductsLoaded()
        }
        .alert("Thank you! 🧡", isPresented: $showThanks) {
            Button("You're welcome", role: .cancel) {}
        } message: {
            Text("Your tip genuinely helps keep Gravitile independent.")
        }
    }

    private func binding(_ keyPath: WritableKeyPath<Settings, Bool>) -> Binding<Bool> {
        Binding(
            get: { appModel.settings[keyPath: keyPath] },
            set: { newValue in
                var settings = appModel.settings
                settings[keyPath: keyPath] = newValue
                appModel.settings = settings
            }
        )
    }

    private var appVersion: String {
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0"
        return version
    }
}
