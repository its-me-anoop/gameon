import AVFoundation

/// Plays the synthesized effects. Uses the `.ambient` category so the game
/// respects the ring/silent switch and never interrupts the player's music.
@MainActor
final class SoundService {
    private var players: [String: AVAudioPlayer] = [:]
    private var musicPlayer: AVAudioPlayer?

    var isEnabled = true
    var isMusicEnabled = true {
        didSet { syncMusic() }
    }

    private static let effects = [
        "place", "upgrade", "demolish", "tap", "denied", "repair",
        "meteor", "catch", "quake", "collapse",
    ]

    init() {
        try? AVAudioSession.sharedInstance().setCategory(.ambient, options: [.mixWithOthers])
        for name in Self.effects {
            guard let url = Bundle.main.url(forResource: name, withExtension: "wav"),
                  let player = try? AVAudioPlayer(contentsOf: url)
            else { continue }
            player.prepareToPlay()
            player.volume = 0.7
            players[name] = player
        }
        if let url = Bundle.main.url(forResource: "drift", withExtension: "wav"),
           let player = try? AVAudioPlayer(contentsOf: url) {
            player.numberOfLoops = -1
            player.volume = 0.2
            player.prepareToPlay()
            musicPlayer = player
        }
        syncMusic()
    }

    func place() { play("place") }
    func upgrade() { play("upgrade") }
    func demolish() { play("demolish") }
    func tap() { play("tap", volume: 0.45) }
    func denied() { play("denied") }
    func repair() { play("repair") }
    func meteorApproach() { play("meteor") }
    func meteorCatch() { play("catch") }
    func quake() { play("quake") }
    func collapse() { play("collapse") }

    private func play(_ name: String, volume: Float = 0.7) {
        guard isEnabled, let player = players[name] else { return }
        player.volume = volume
        player.currentTime = 0
        player.play()
    }

    private func syncMusic() {
        guard let musicPlayer else { return }
        if isMusicEnabled {
            if !musicPlayer.isPlaying { musicPlayer.play() }
        } else {
            musicPlayer.pause()
        }
    }
}
