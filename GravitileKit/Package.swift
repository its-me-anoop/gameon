// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GravitileKit",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "GravitileKit", targets: ["GravitileKit"]),
        .library(name: "OrbitKit", targets: ["OrbitKit"]),
        .executable(name: "BalanceSim", targets: ["BalanceSim"]),
        .executable(name: "OrbitSim", targets: ["OrbitSim"]),
    ],
    targets: [
        .target(name: "GravitileKit"),
        .target(name: "OrbitKit"),
        .executableTarget(name: "BalanceSim", dependencies: ["GravitileKit"]),
        .executableTarget(name: "OrbitSim", dependencies: ["OrbitKit"]),
        .testTarget(name: "GravitileKitTests", dependencies: ["GravitileKit"]),
        .testTarget(name: "OrbitKitTests", dependencies: ["OrbitKit"]),
    ]
)
