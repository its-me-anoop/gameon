import XCTest

@MainActor
final class GravitileUITests: XCTestCase {
    private func launch() -> XCUIApplication {
        let app = XCUIApplication()
        app.launchArguments = ["-gravitile-reset"]
        app.launch()
        return app
    }

    func testHomeShowsCoreActions() {
        let app = launch()
        XCTAssertTrue(app.buttons["playEndless"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.buttons["playDaily"].exists)
        XCTAssertTrue(app.buttons["statsLink"].exists)
        XCTAssertTrue(app.buttons["settingsLink"].exists)
    }

    func testEndlessGameAcceptsSwipesAndTracksState() {
        let app = launch()
        app.buttons["playEndless"].tap()

        let board = app.otherElements["board"]
        XCTAssertTrue(board.waitForExistence(timeout: 5))

        let undo = app.buttons["undoButton"]
        XCTAssertFalse(undo.isEnabled, "Undo must start disabled")

        // A fresh board always has at least one legal move among these.
        board.swipeLeft()
        board.swipeDown()
        board.swipeRight()

        let undoEnabled = NSPredicate(format: "isEnabled == true")
        let becameEnabled = XCTNSPredicateExpectation(predicate: undoEnabled, object: undo)
        XCTAssertEqual(XCTWaiter().wait(for: [becameEnabled], timeout: 5), .completed,
                       "Undo enables once a move commits")

        XCTAssertTrue(app.otherElements["gravityCompass"].exists)
    }

    func testDailyScreenOffersTodayPuzzle() {
        let app = launch()
        app.buttons["playDaily"].tap()
        // List rows may expose the link as a cell rather than a button.
        let playToday = app.descendants(matching: .any)["playToday"].firstMatch
        XCTAssertTrue(playToday.waitForExistence(timeout: 5))
        playToday.tap()
        XCTAssertTrue(app.otherElements["board"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.otherElements["movesRemaining"].firstMatch.exists
                      || app.staticTexts["movesRemaining"].firstMatch.exists)
    }

    func testPaywallReachableFromSettings() {
        let app = launch()
        app.buttons["settingsLink"].tap()
        let plusRow = app.buttons["Unlock Plus"].firstMatch
        XCTAssertTrue(plusRow.waitForExistence(timeout: 5))
        plusRow.tap()
        XCTAssertTrue(app.buttons["buyPlus"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.staticTexts["Gravitile Plus"].exists)
    }
}
