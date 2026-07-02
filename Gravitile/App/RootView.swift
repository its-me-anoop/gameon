import SwiftUI

struct RootView: View {
    var body: some View {
        VStack(spacing: 12) {
            Text("Gravitile")
                .font(.largeTitle.weight(.bold))
            Text("Scaffold build")
                .foregroundStyle(.secondary)
        }
        .accessibilityIdentifier("rootTitle")
    }
}

#Preview {
    RootView()
}
