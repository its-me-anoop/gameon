using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        public ClinicCommandResult UpgradeStation(ClinicStaffRole role, int stationId)
        {
            if (!TutorialComplete() || !Defined(role)) return No("Finish the first treatment before upgrading.");
            var room = role == ClinicStaffRole.Receptionist ? ClinicRoom.Reception : ClinicRoom.FirstAid;
            var level = ClinicRules.StationLevel(State, role, stationId);
            if (level < 1) return No("Build this workstation first.");
            if (level >= ClinicRules.TrackCap(State.Room(room).Tier)) return No("Upgrade the room tier for better workstations.");
            var cost = ClinicRules.StationUpgradeCost(State, role, stationId);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for this workstation.");
            Spend(cost);
            if (role == ClinicStaffRole.Receptionist) State.ReceptionDesks.Find(d => d.Id == stationId).EquipmentLevel++;
            else State.TreatmentStations.Find(s => s.Id == stationId).EquipmentLevel++;
            Emit(ClinicEventKind.StationUpgraded, room, deskId: role == ClinicStaffRole.Receptionist ? stationId : -1,
                amount: cost, source: role == ClinicStaffRole.Receptionist ? ClinicRules.DeskPatientAnchor(stationId) : ClinicRules.TreatmentPatientAnchor(stationId));
            return Yes("Workstation improved.", cost);
        }

        public ClinicCommandResult TrainStaff(int staffId)
        {
            if (!TutorialComplete()) return No("Finish the first treatment before training staff.");
            var staff = State.Staff.Find(s => s.Id == staffId);
            if (staff == null) return No("Hire this staff member first.");
            var room = staff.Role == ClinicStaffRole.Receptionist ? ClinicRoom.Reception : ClinicRoom.FirstAid;
            if (staff.TrainingLevel >= ClinicRules.TrackCap(State.Room(room).Tier)) return No("Upgrade the room tier for further training.");
            var cost = ClinicRules.StaffTrainingCost(staff);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for training.");
            Spend(cost);
            staff.TrainingLevel++;
            Emit(ClinicEventKind.StaffTrained, room, staffId: staffId, amount: cost, source: staff.ToAnchor);
            return Yes("Staff training complete.", cost);
        }

        public ClinicCommandResult UpgradeAmenity(ClinicAmenity kind)
        {
            if (!TutorialComplete() || !Defined(kind)) return No("Finish the first treatment before expanding.");
            var amenity = State.Amenity(kind);
            var waiting = State.Room(ClinicRoom.Waiting);
            if (kind != ClinicAmenity.Parking && !waiting.Built) return No("Build the waiting room first.");
            var cap = kind == ClinicAmenity.Parking ? 3 : waiting.Tier;
            if (amenity.Level >= cap) return No(cap == 3 ? "Fully upgraded." : "Upgrade the waiting room for more improvements.");
            var cost = ClinicRules.AmenityUpgradeCost(kind, amenity.Level);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for this improvement.");
            Spend(cost);
            amenity.Level++;
            Emit(ClinicEventKind.AmenityUpgraded, ClinicRoom.Waiting, amount: cost,
                source: kind == ClinicAmenity.Parking ? "parking.plot" : ClinicRules.AmenityPatientAnchor(kind), amenity: kind);
            return Yes("Amenity improved.", cost);
        }

        public ClinicCommandResult CollectVendingTips()
        {
            var amenity = State.Amenity(ClinicAmenity.Vending);
            var amount = amenity.Till;
            if (amount <= 0) return No("Tips collect here after patients visit.");
            if (State.Wallet > MaximumMoney - amount) return No("The clinic wallet is full.");
            State.Wallet += amount;
            State.TotalCollected += amount;
            amenity.Till = 0;
            Emit(ClinicEventKind.CashCollected, ClinicRoom.Waiting, amount: amount,
                source: "waiting.vending.cash", amenity: ClinicAmenity.Vending);
            return Yes("Tips collected.", amount: amount);
        }

        private void DispatchAmenities()
        {
            if (!State.Room(ClinicRoom.Waiting).Built) return;
            // DispatchNurses runs first. A patient only uses a free amenity while all available nurses are busy.
            foreach (var patient in State.Patients.Where(p => p.Phase == ClinicPatientPhase.Seated).OrderBy(p => p.Id))
            {
                if (!patient.UsedToilet && patient.Id % 3 == 1 && AmenityAvailable(ClinicAmenity.Toilet))
                    StartAmenityVisit(patient, ClinicAmenity.Toilet);
                else if (!patient.UsedVending && AmenityAvailable(ClinicAmenity.Vending))
                    StartAmenityVisit(patient, ClinicAmenity.Vending);
            }
        }

        private bool AmenityAvailable(ClinicAmenity kind) => State.Amenity(kind).Level > 0
            && !State.Patients.Any(p => IsVisitingAmenity(p) && p.VisitingAmenity == kind);

        private void StartAmenityVisit(ClinicPatientState patient, ClinicAmenity kind)
        {
            patient.VisitingAmenity = kind;
            Phase(patient, ClinicPatientPhase.WalkingToAmenity, patient.ToAnchor, ClinicRules.AmenityPatientAnchor(kind), 30);
            Emit(ClinicEventKind.AmenityVisitStarted, ClinicRoom.Waiting, patient.Id, source: patient.ToAnchor, amenity: kind);
        }

        private void FinishAmenityVisit(ClinicPatientState patient)
        {
            if (patient.VisitingAmenity == ClinicAmenity.Toilet) patient.UsedToilet = true;
            else if (!patient.UsedVending)
            {
                var amount = ClinicRules.VendingTipForPatient(State, patient);
                patient.UsedVending = true;
                if (amount > 0)
                {
                    State.Amenity(ClinicAmenity.Vending).Till = checked(State.Amenity(ClinicAmenity.Vending).Till + amount);
                    State.TotalTips = checked(State.TotalTips + amount);
                    State.TotalEarned = checked(State.TotalEarned + amount);
                    patient.TipPaid = amount;
                    Emit(ClinicEventKind.TipReceived, ClinicRoom.Waiting, patient.Id, amount: amount,
                        source: "waiting.vending.cash", amenity: ClinicAmenity.Vending);
                }
            }
            Phase(patient, ClinicPatientPhase.ReturningFromAmenity, patient.ToAnchor, ClinicRules.WaitingAnchor(true, patient.SeatId), 30);
        }
    }
}
