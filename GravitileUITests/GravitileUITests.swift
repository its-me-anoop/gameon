import XCTest

final class GravitileUITests: XCTestCase {
    func testAppLaunches() {
        let app = XCUIApplication()
        app.launch()
        XCTAssertTrue(app.staticTexts["rootTitle"].waitForExistence(timeout: 5))
    }
}
