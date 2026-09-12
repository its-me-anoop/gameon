using System;
using System.Collections.Generic;

namespace IdleClinic.Core
{
    public enum ClinicLocation { StarterClinic = 0, DoctorsClinic = 1 }
    public enum ClinicRoom { Reception, FirstAid, Waiting, Consultation, Pharmacy }
    public enum UpgradeTrack { Equipment, Facilities, Decoration }
    public enum ClinicStaffRole { Receptionist, Nurse, Doctor, Pharmacist }
    public enum ClinicAmenity { Parking, Toilet, Vending, Taxi }
    public enum ClinicTutorialStep { FirstArrival, CollectFirstPayment, HireFirstNurse, FirstTreatment, Complete }
    public enum ClinicPatientPhase
    {
        Arriving, ReceptionQueue, WalkingToReception, CheckingIn, WaitingForTreatment,
        WalkingToWaiting, Seated, WalkingToTreatment, Treating, Leaving,
        WalkingToAmenity, UsingAmenity, ReturningFromAmenity,
        WaitingToPark, DrivingToParking, WaitingToExit, DrivingFromParking,
        WalkingToConsultation, Consulting, WalkingToPharmacy, Dispensing,
        WaitingForTaxi, TaxiArriving, TaxiDroppingOff, WalkingToTaxi, TaxiPickingUp, TaxiDeparting
    }
    public enum ClinicTaxiPhase { Approaching, Boarding, Departing, WaitingToDepart }
    public enum ClinicConstructionKind { WaitingRoom, RoomRenovation }
    public enum ClinicEventKind
    {
        PatientArrived, CheckInStarted, PaymentReceived, CashCollected, NurseHired,
        ReceptionistHired, TreatmentStarted, TreatmentCompleted, WaitingRoomUnlocked,
        ConstructionStarted, ConstructionCompleted, EquipmentUpgraded, StationAdded,
        TutorialAdvanced, StationUpgraded, StaffTrained, AmenityUpgraded, AmenityVisitStarted, TipReceived,
        DoctorHired, PharmacistHired, ConsultationStarted, ConsultationCompleted,
        DispensingStarted, DispensingCompleted, DoctorsClinicUnlocked, TaxiArrived, TaxiDeparted
    }

    [Serializable]
    public sealed class ClinicState
    {
        public int SchemaVersion = 3;
        public int RulesVersion = 3;
        public ClinicLocation Location;
        public bool DoctorsClinicUnlocked;
        public long TotalTransferredIn;
        public long TotalTransferredOut;
        public ulong Seed = 42;
        public long Tick;
        public long PausedTrafficTicks;
        public double SubTick;
        public long Wallet;
        public long NextArrivalTick = -1;
        public int NextPatientId;
        public long NextEventId = 1;
        public int NextConstructionId;
        public long TotalPayments;
        public long TotalCollected;
        public long TotalEarned;
        public long TotalSpent;
        public long TotalTreatments;
        public long TotalTips;
        public bool WaitingRoomUnlocked;
        public ClinicTutorialStep Tutorial;
        public List<ClinicRoomState> Rooms = new List<ClinicRoomState>();
        public List<ReceptionDeskState> ReceptionDesks = new List<ReceptionDeskState>();
        public List<TreatmentStationState> TreatmentStations = new List<TreatmentStationState>();
        public List<TreatmentStationState> ConsultationStations = new List<TreatmentStationState>();
        public List<TreatmentStationState> PharmacyStations = new List<TreatmentStationState>();
        public List<ClinicAmenityState> Amenities = new List<ClinicAmenityState>();
        public List<ClinicStaffState> Staff = new List<ClinicStaffState>();
        public List<ClinicTaxiState> TaxiRides = new List<ClinicTaxiState>();
        public List<ClinicPatientState> Patients = new List<ClinicPatientState>();
        public List<ClinicConstructionState> Construction = new List<ClinicConstructionState>();

        public ClinicAmenityState Amenity(ClinicAmenity kind) => Amenities.Find(a => a.Kind == kind);
        public ClinicRoomState Room(ClinicRoom kind) => Rooms.Find(r => r.Kind == kind);
        public ClinicRoomState GetRoom(ClinicRoom kind) => Room(kind);
    }

    [Serializable]
    public sealed class ClinicRoomState
    {
        public ClinicRoom Kind;
        public bool Built;
        public int Tier = 1;
        public int EquipmentLevel = 1;
        public int FacilitiesLevel = 1;
        public int DecorationLevel = 1;
        public int StationCount;
        public int Level(UpgradeTrack track) => track == UpgradeTrack.Equipment ? EquipmentLevel
            : track == UpgradeTrack.Facilities ? FacilitiesLevel : DecorationLevel;
    }

    [Serializable]
    public sealed class ReceptionDeskState
    {
        public int Id;
        public long Till;
        public int EquipmentLevel = 1;
        public int PatientId = -1;
        public long LastStartedTick = -1;
    }

    [Serializable]
    public sealed class TreatmentStationState
    {
        public int Id;
        public int EquipmentLevel = 1;
    }

    [Serializable]
    public sealed class ClinicAmenityState
    {
        public ClinicAmenity Kind;
        public int Level;
        public long Till;
    }

    [Serializable]
    public sealed class ClinicStaffState
    {
        public int Id;
        public ClinicStaffRole Role;
        public int TrainingLevel = 1;
        public int StationId;
        public int PatientId = -1;
        public string FromAnchor = "entrance";
        public string ToAnchor = "entrance";
        public long MoveStartedTick;
        public long MoveEndsTick;
    }

    [Serializable]
    public sealed class ClinicPatientState
    {
        public int Id;
        public List<ClinicMovementPoint> ArrivalPath = new List<ClinicMovementPoint>();
        public List<ClinicMovementPoint> QueueMovePath = new List<ClinicMovementPoint>();
        public long QueueMoveStartedTick;
        public long QueueMoveEndsTick;
        public ClinicPatientPhase Phase;
        public int AppearanceId;
        public int ParkingBayId = -1;
        public int TaxiDockId = -1;
        public bool UsesTaxi;
        public int ToiletCubicleId = -1;
        public int ConsultationStationId = -1;
        public int PharmacyStationId = -1;
        public ClinicStaffRole NextService = ClinicStaffRole.Nurse;
        public bool ConsultationComplete;
        public bool FirstAidComplete;
        public bool PharmacyComplete;
        public ClinicAmenity VisitingAmenity;
        public bool UsedToilet;
        public bool UsedVending;
        public long TipPaid;
        public long ArrivalTick;
        public long PhaseStartedTick;
        public long PhaseEndsTick;
        public string FromAnchor = "entrance";
        public string ToAnchor = "entrance";
        public int DeskId = -1;
        public int TreatmentStationId = -1;
        public int SeatId = -1;
        public int QueueIndex = -1;
        public bool Paid;
        public long Payment;
        public bool HasAdmissionReservation;
    }

    [Serializable]
    public sealed class ClinicMovementPoint
    {
        public float X;
        public float Z;
        public ClinicMovementPoint() { }
        public ClinicMovementPoint(float x, float z) { X = x; Z = z; }
    }

    [Serializable]
    public sealed class ClinicTaxiState
    {
        public long Id;
        public int PatientId;
        public int DockId;
        public bool Pickup;
        public ClinicTaxiPhase Phase;
        public long PhaseStartedTick;
        public long PhaseEndsTick;
    }

    [Serializable]
    public sealed class ClinicConstructionState
    {
        public int Id;
        public ClinicRoom Room;
        public ClinicConstructionKind Kind;
        public int TargetTier;
        public long StartedTick;
        public long EndsTick;
        public long PaidCost;
    }

    public sealed class ClinicEvent
    {
        public long Id;
        public long Tick;
        public ClinicEventKind Kind;
        public int PatientId = -1;
        public int StaffId = -1;
        public int DeskId = -1;
        public ClinicRoom Room;
        public ClinicAmenity Amenity;
        public string SourceAnchor = "";
        public long Amount;
    }

    public readonly struct ClinicCommandResult
    {
        public bool Success { get; }
        public string Message { get; }
        public long Cost { get; }
        public long Amount { get; }
        public ClinicCommandResult(bool success, string message, long cost = 0, long amount = 0)
        { Success = success; Message = message; Cost = cost; Amount = amount; }
    }

    public sealed class ClinicAdvanceReport
    {
        public double Seconds;
        public double EarningsSeconds;
        public double ConstructionSeconds;
        public bool WasCapped;
        public long PaymentsReceived;
        public long TillEarned;
        public long TreatmentsCompleted;
        public List<ClinicEvent> Events = new List<ClinicEvent>();
    }
}
