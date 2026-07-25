// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GravitileKit",
    platforms: [.iOS(.v18), .macOS(.v14)],
    products: [
        .library(name: "OrbitKit", targets: ["OrbitKit"]),
        .executable(name: "OrbitSim", targets: ["OrbitSim"]),
    ],
    targets: [
        .target(name: "OrbitKit"),
        .executableTarget(name: "OrbitSim", dependencies: ["OrbitKit"]),
        .testTarget(name: "OrbitKitTests", dependencies: ["OrbitKit"]),
    ]
)
