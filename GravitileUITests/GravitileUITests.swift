import XCTest

/// Smoke tests. The world is a rendered scene, so these check that the app
/// launches, the interface is present, and the tray responds — not pixels.
final class GravitileUITests: XCTestCase {
    override func setUpWithError() throws {
        continueAfterFailure = false
    }

    private func launch() -> XCUIApplication {
        let app = XCUIApplication()
        app.launchArguments = ["-gravitile-reset"]
        app.launch()
        return app
    }

    func testLaunchesIntoTheTutorialAndReachesTheWorld() {
        let app = launch()
        let start = app.buttons["Next"]
        XCTAssertTrue(start.waitForExistence(timeout: 10))
        for _ in 0..<3 where app.buttons["Next"].exists {
            app.buttons["Next"].tap()
        }
        app.buttons["Start"].tap()
        XCTAssertTrue(app.buttons["Solar Array"].waitForExistence(timeout: 5))
    }

    func testMenuOpensAndCloses() {
        let app = launch()
        for _ in 0..<3 where app.buttons["Next"].exists { app.buttons["Next"].tap() }
        if app.buttons["Start"].exists { app.buttons["Start"].tap() }

        app.buttons["Menu"].tap()
        XCTAssertTrue(app.staticTexts["RECORDS"].waitForExistence(timeout: 5))
        app.buttons["Done"].tap()
        XCTAssertTrue(app.buttons["Menu"].waitForExistence(timeout: 5))
    }

    func testArmingAMachineShowsThePlacementPrompt() {
        let app = launch()
        for _ in 0..<3 where app.buttons["Next"].exists { app.buttons["Next"].tap() }
        if app.buttons["Start"].exists { app.buttons["Start"].tap() }

        app.buttons["Solar Array"].tap()
        XCTAssertTrue(app.staticTexts["Tap the world to place"].waitForExistence(timeout: 3))
        app.buttons["Cancel"].tap()
        XCTAssertFalse(app.staticTexts["Tap the world to place"].exists)
    }
}
