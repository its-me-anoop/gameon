# Little Lifeline 3.3 (17)

The doctors clinic opens after fully upgrading the starter clinic and paying 100,000 coins. The new location doubles room area and corresponding income, prices and service/build times, with four receptionists, four doctors, four nurses, two pharmacists, six room tiers and twelve component, station and training levels. Both clinics retain their progression when travelling.

The expanded grounds include twelve parking bays, separate entrance/exit lanes, a taxi stand, two toilet cubicles and a larger waiting room. Consultation, first aid and pharmacy are distinct patient stages. Queued arrivals retain their remaining physical path when their destination changes, including after saving and reopening. Queue advancement uses saved walking paths, and reception admits a settled patient after the preceding visitor has cleared the aisle.

An original 48-second background score and thirteen action sounds accompany payments, collection, staff purchases, care, construction, doors, walking and taxis. Music and effects have separate persistent settings. Pausing preserves the music position, and offline earnings do not replay historical effects.

## Candidate validation

- Unity EditMode: **643/643 passed**, zero failures or skipped cases (`build/qa-doctors-clinic/tests-05.xml`).
- Export packaging: **27/27 passed**.
- Actual Unity GPU previews were inspected for room walls/doors, workstation separation, seating, parking and taxi geometry. These rendering fixtures do not establish runtime input or economy progression.
- Earned native test campaigns were produced using normal production simulation commands and round-tripped through the profile store. No test currency was minted.
- The previous native candidate exposed a queue detour and was stopped. The corrected navigation and rendered-world tests pass **77/77**; fresh simulator input, native audio output, physical iPhone performance and TestFlight availability remain pending for this corrected source.
- The release pipeline can compile with released Xcode, use an existing local signing identity, and independently verify the signed archive without exporting the private key or revoking a certificate. This route still requires its first complete archive/export validation.

Schema 3 preserves the original starter field, stores the optional doctors campaign as an explicit empty/singleton list, validates the paired money ledgers, and atomically saves both locations and preferences. Real 3.1 and 3.2 saves are covered by migration tests; the old source file is archived before migration.

New leaderboards and monetisation remain deferred. This release does not submit an App Store review or enable external beta testing.
