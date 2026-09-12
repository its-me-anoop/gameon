using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        private static bool IsValidExpansion(ClinicState state)
        {
            if (state.TotalTips < 0 || state.TotalTips > 21 * state.TotalPayments || state.TotalTips > state.TotalEarned
                || state.Amenities == null || state.Amenities.Count != 3 || state.TreatmentStations == null
                || state.TreatmentStations.Count != state.Room(ClinicRoom.FirstAid).StationCount) return false;
            var kinds = new HashSet<ClinicAmenity>();
            foreach (var amenity in state.Amenities)
            {
                if (amenity == null || !Defined(amenity.Kind) || !kinds.Add(amenity.Kind) || amenity.Level < 0 || amenity.Level > 3
                    || amenity.Till < 0 || amenity.Till > state.TotalTips || amenity.Kind != ClinicAmenity.Vending && amenity.Till != 0
                    || amenity.Level == 0 && amenity.Till != 0 || amenity.Level > 0 && state.Tutorial != ClinicTutorialStep.Complete) return false;
                if (amenity.Kind != ClinicAmenity.Parking && amenity.Level > 0
                    && (!state.Room(ClinicRoom.Waiting).Built || amenity.Level > state.Room(ClinicRoom.Waiting).Tier)) return false;
            }
            if (state.Amenity(ClinicAmenity.Vending).Level == 0 && state.TotalTips != 0) return false;
            var stationIds = new HashSet<int>();
            foreach (var station in state.TreatmentStations)
                if (station == null || station.Id < 0 || station.Id >= state.TreatmentStations.Count || !stationIds.Add(station.Id)
                    || station.EquipmentLevel < 1 || station.EquipmentLevel > ClinicRules.TrackCap(state.Room(ClinicRoom.FirstAid).Tier)) return false;
            var bays = new HashSet<int>();
            var visitors = new HashSet<ClinicAmenity>();
            int movingVehicles=0;
            long currentTips = 0;
            foreach (var patient in state.Patients)
            {
                if (patient == null || patient.AppearanceId != ClinicRules.PatientAppearance(state.Seed, patient.Id)
                    || patient.ParkingBayId < -1 || patient.ParkingBayId >= ClinicRules.ParkingCapacity(state)
                    || patient.ParkingBayId >= 0 && (patient.Id == 0 || patient.Id % 3 != 0 || !bays.Add(patient.ParkingBayId))
                    || !Defined(patient.VisitingAmenity) || patient.TipPaid < 0 || patient.TipPaid > 21
                    || !patient.UsedVending && patient.TipPaid != 0 || patient.UsedVending && patient.TipPaid > 0 && patient.TipPaid < 5
                    || patient.UsedVending && (!patient.Paid || state.Amenity(ClinicAmenity.Vending).Level == 0)
                    || patient.UsedToilet && (!patient.Paid || patient.Id % 3 != 1 || state.Amenity(ClinicAmenity.Toilet).Level == 0)) return false;
                currentTips += patient.TipPaid;
                if(IsVehiclePhase(patient.Phase))
                {
                    if(patient.ParkingBayId<0)return false;
                    if(IsMovingVehicle(patient.Phase)&&++movingVehicles>1)return false;
                    bool entry=patient.Phase==ClinicPatientPhase.WaitingToPark||patient.Phase==ClinicPatientPhase.DrivingToParking;
                    if(entry&&(patient.Paid||patient.FromAnchor!=ClinicRules.ParkingPatientAnchor(patient.ParkingBayId)||patient.ToAnchor!=ClinicRules.QueueAnchor(patient.QueueIndex)))return false;
                    if(!entry&&(!patient.Paid||patient.FromAnchor!=ClinicRules.ParkingPatientAnchor(patient.ParkingBayId)||patient.ToAnchor!=patient.FromAnchor))return false;
                    if(patient.Phase==ClinicPatientPhase.DrivingToParking&&patient.PhaseEndsTick-patient.PhaseStartedTick!=ClinicRules.ParkingEntryTicks)return false;
                    if(patient.Phase==ClinicPatientPhase.DrivingFromParking&&patient.PhaseEndsTick-patient.PhaseStartedTick!=ClinicRules.ParkingExitTicks)return false;
                }
                var bayAnchor = patient.ParkingBayId >= 0 ? ClinicRules.ParkingPatientAnchor(patient.ParkingBayId) : null;
                if (patient.FromAnchor != null && patient.FromAnchor.StartsWith("parking.", StringComparison.Ordinal) && patient.FromAnchor != bayAnchor
                    || patient.ToAnchor != null && patient.ToAnchor.StartsWith("parking.", StringComparison.Ordinal) && patient.ToAnchor != bayAnchor) return false;
                if (patient.ParkingBayId >= 0)
                {
                    if (patient.Phase == ClinicPatientPhase.Arriving && patient.FromAnchor != ClinicRules.ParkingPatientAnchor(patient.ParkingBayId)) return false;
                    if (patient.Phase == ClinicPatientPhase.Leaving && patient.ToAnchor != ClinicRules.ParkingPatientAnchor(patient.ParkingBayId)) return false;
                    if (patient.Payment > 0 && patient.Payment < 55) return false;
                }
                if (!IsVisitingAmenity(patient)) continue;
                if (!patient.Paid || !patient.HasAdmissionReservation || patient.SeatId < 0 || patient.TreatmentStationId != -1
                    || patient.VisitingAmenity == ClinicAmenity.Parking || state.Amenity(patient.VisitingAmenity).Level == 0
                    || patient.VisitingAmenity == ClinicAmenity.Toilet && patient.Id % 3 != 1
                    || !visitors.Add(patient.VisitingAmenity)) return false;
                var anchor = ClinicRules.AmenityPatientAnchor(patient.VisitingAmenity);
                if (patient.Phase == ClinicPatientPhase.ReturningFromAmenity)
                {
                    if (patient.FromAnchor != anchor || patient.ToAnchor != ClinicRules.WaitingAnchor(true, patient.SeatId)
                        || patient.VisitingAmenity == ClinicAmenity.Toilet && !patient.UsedToilet
                        || patient.VisitingAmenity == ClinicAmenity.Vending && !patient.UsedVending) return false;
                }
                else if (patient.ToAnchor != anchor || patient.Phase == ClinicPatientPhase.UsingAmenity && patient.FromAnchor != anchor
                    || patient.VisitingAmenity == ClinicAmenity.Toilet && patient.UsedToilet
                    || patient.VisitingAmenity == ClinicAmenity.Vending && patient.UsedVending) return false;
            }
            return currentTips <= state.TotalTips;
        }
    }
}
