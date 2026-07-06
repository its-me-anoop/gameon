import AVFoundation

/// Plays the synthesized effects. Uses the `.ambient` category so the game
/// respects the ring/silent switch and never interrupts the user's music.
@MainActor
final class SoundService {
    private var players: [String: AVAudioPlayer] = [:]
    private var musicPlayer: AVAudioPlayer?
    /// Whether a screen currently wants music (independent of the setting).
    private var musicRequested = false

    var isEnabled = true
    var isMusicEnabled = true {
        didSet { syncMusic() }
    }

    init() {
        try? AVAudioSession.sharedInstance().setCategory(.ambient, options: [.mixWithOthers])
        let effects = [
            "merge1", "merge2", "merge3", "merge4", "merge5",
            "slide", "gameover", "fanfare",
            "whoosh", "land", "tap", "milestone", "newbest",
            "chip", "shatter",
        ]
        for name in effects {
            if let url = Bundle.main.url(forResource: name, withExtension: "wav"),
               let player = try? AVAudioPlayer(contentsOf: url) {
                player.prepareToPlay()
                player.volume = 0.7
                players[name] = player
            }
        }
        if let url = Bundle.main.url(forResource: "bgm", withExtension: "wav"),
           let player = try? AVAudioPlayer(contentsOf: url) {
            player.numberOfLoops = -1
            player.volume = 0.22
            player.prepareToPlay()
            musicPlayer = player
        }
    }

    func merge(round: Int) {
        play("merge\(min(max(round, 1), 5))")
    }

    func slide() { play("slide") }
    func gameOver() { play("gameover") }
    func fanfare() { play("fanfare") }
    func whoosh() { play("whoosh") }
    func land() { play("land") }
    func tap() { play("tap") }
    func milestone() { play("milestone") }
    func newBest() { play("newbest") }
    func chip() { play("chip") }
    func shatter() { play("shatter") }

    // MARK: - Music

    /// Screens opt in on appear / out on disappear; the toggle and scene
    /// phase are reconciled in one place so state can't drift.
    func startMusic() {
        musicRequested = true
        syncMusic()
    }

    func stopMusic() {
        musicRequested = false
        syncMusic()
    }

    /// Re-assert after returning to the foreground — ambient players don't
    /// resume themselves after the session deactivates in the background.
    func syncMusic() {
        guard let musicPlayer else { return }
        if musicRequested && isMusicEnabled {
            if !musicPlayer.isPlaying { musicPlayer.play() }
        } else if musicPlayer.isPlaying {
            musicPlayer.pause()
        }
    }

    private func play(_ name: String) {
        guard isEnabled, let player = players[name] else { return }
        player.currentTime = 0
        player.play()
    }
}
