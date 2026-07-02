// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GravitileKit",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [.library(name: "GravitileKit", targets: ["GravitileKit"])],
    targets: [
        .target(name: "GravitileKit"),
        .testTarget(name: "GravitileKitTests", dependencies: ["GravitileKit"]),
    ]
)
