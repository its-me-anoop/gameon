# Clinic neighbourhood and individual progression

This expansion continues the existing clinic save. Version 3.2 (16) adds varied people, street activity, parking, waiting-area amenities, individual workstation upgrades, staff training and visible construction. New leaderboards and real-money purchases remain deferred.

## World and interaction

The clinic and neighbourhood use the existing ivory, sage, apricot and timber palette. Floor finishes, rugs, supplies, storage, notice boards, plants and street furniture fill unused areas. Paths remain legible and traversable. Privacy partitions, the named entrance and the four-staff limit remain.

Patients and staff have deterministic appearances, with variations in clothing, skin tone, hair, accessories and proportions. Pooled actors must reset their appearance when reused. Seated poses and treatment positions remain aligned with furniture across body sizes.

Cars use the streets and parking bays; pedestrians use pavements and the crossing. The parking area has a safe path to the entrance. Ambient traffic is bounded and does not create clinic income. Reduced motion suppresses cosmetic traffic and construction motion while preserving essential simulation feedback.

Each desk and nursing station opens its own compact controls, including its equipment and assigned staff member's training. Room controls retain shared equipment, facilities, decoration and renovation. Parking, toilet and vending objects open their own upgrade controls. Cash markers take priority over selection only at the actual cash location. All controls retain 44-point minimum targets and cancel activation after dragging or pinching.

## Additional progression

The existing opening, shared room upgrades and spending remain valid. New purchases unlock after the first treatment. Prices use whole coins and overflow-safe arithmetic.

| Upgrade | Starting price | Price growth | Limit |
| --- | ---: | --- | --- |
| Individual reception desk | 90 | ×1.8 per previous level | 2 / 4 / 6 by room tier |
| Individual nursing station | 110 | ×1.8 per previous level | 2 / 4 / 6 by room tier |
| Receptionist training | 80 | ×1.8 per previous level | 2 / 4 / 6 by room tier |
| Nurse training | 100 | ×1.8 per previous level | 2 / 4 / 6 by room tier |
| Car park | 220 | ×2 per previous purchase | Three levels; 2 / 4 / 6 bays |
| Waiting-area toilet | 140 | ×2 per previous purchase | Waiting-room tier, up to three |
| Waiting-area vending machine | 180 | ×2 per previous purchase | Waiting-room tier, up to three |

Workstation and training levels begin at 1. The service-time denominator adds the existing shared-equipment bonus of 15% per level above 1, the individual workstation bonus of 10% per level above 1, and staff training of 12% per level above 1. Existing in-progress services keep their captured deadlines. Equipment and training apply to the corresponding workstation and employee only.

Amenities begin unbuilt at level 0. The toilet and vending machine require the waiting room. Their visual fixtures improve at every level.

Eligible arriving patients reserve a free parking bay for the whole visit and walk from it to reception, then return after care. The parking fee is five coins per car-park level, included in the patient's fixed check-in quote. Bay occupancy never produces revenue on its own.

Paid waiting patients may use the toilet or vending machine while care stations are occupied. Treatment assignment has priority over starting a leisure visit. Patients keep their seat and admission reservations during these visits. The toilet has one occupant at a time and gets faster with upgrades. A vending visit can produce one tip of five coins per machine level, plus a comfort bonus of two coins per toilet level for a patient who used it. Tips accumulate at the machine until collected. The collection command transfers the balance once; the coin animation never grants money.

Room renovation shows scaffolding, tools and workers only while an authoritative construction job is active. Progress derives from its saved start and end ticks. Care continues during renovation, and the scaffolding clears when the new tier activates.

## Save and validation requirements

Validate the original checksummed envelope before migrating schema/rules version 1 into version 2. Preserve wallet, tills, staff, room upgrades, patients, reservations, construction, tutorial, preferences and time accounting. Commit the migrated profile atomically before offline processing. New equipment and training levels start at 1; optional amenities start unbuilt. Preserve legacy campaign and purchase ownership behavior.

Vending tips participate in earned/collected/spent/till conservation and the eight-hour offline earnings cap. Construction retains full elapsed-time completion. Relaunching, interrupting an animation or collecting twice cannot repeat a tip or a parking fee.

Acceptance includes full Unity tests, real 3.1-save migration, independent workstation/training effects, caps and exponential costs, distinct parking/amenity reservations, correct release of bays, bounded traffic, visible construction, gesture-safe collection and selection, small-screen native input, and recorded visual review. Simulator and physical-device evidence remain separate. Release requires a verified production export, signed archive, upload and independent internal TestFlight readback.
