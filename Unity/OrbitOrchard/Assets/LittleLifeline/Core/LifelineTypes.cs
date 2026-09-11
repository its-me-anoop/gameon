using System;
using System.Collections.Generic;

namespace LittleLifeline.Core
{
    public enum SimulationMode { Campaign, Weekly }
    public enum TownId { Willowbank, Copperhill, Seabrook }
    public enum RoomKind { Consultation, Diagnostics, Recovery }
    public enum CarePath { Consultation, Diagnostics, Recovery }
    public enum CrewRole { Doctor, Technician, Nurse }
    public enum PatientPhase { Waiting, Walking, Treating, Departing }
    public enum ServicePolicy { OldestFirst, ShortVisitsFirst, ProjectFirst }

    /// <summary>All persistent simulation data. Tick is one tenth of a second; SubTick is a fraction of a tick.</summary>
    [Serializable]
    public sealed class SimulationState
    {
        public int SchemaVersion = 1;
        public int RulesVersion = 1;
        public SimulationMode Mode;
        public TownId Town;
        public ServicePolicy Policy;
        public ulong Seed;
        public ulong RandomState;
        public long Tick;
        public double SubTick;
        public long NextArrivalTick = 10;
        public int NextPatientId;
        public int NextRoomId = 1;
        public int ArrivalCount;
        public int Coins = 100;
        public int Reputation;
        public int TotalCompleted;
        public long TotalWaitingTicks;
        public string WeeklyId = "";
        public bool IsFinished;
        public int PendingMoveRoomId = -1;
        public int PendingMoveTargetSlot = -1;
        // A false flag makes older schema-one saves safe when these fields are absent.
        public bool HasPendingRefit;
        public int PendingRefitRoomId;
        public RoomKind PendingRefitKind;
        public int PendingRefitCost;
        public List<CarriageState> Carriages = new List<CarriageState>();
        public List<CrewState> Crew = new List<CrewState>();
        public List<PatientState> Patients = new List<PatientState>();
        public List<TownProgress> Towns = new List<TownProgress>();
    }

    [Serializable]
    public sealed class CarriageState
    {
        public int Id;
        public int Slot;
        public RoomKind Kind;
        public int Level = 1;
    }

    [Serializable]
    public sealed class CrewState
    {
        public int Id;
        public string Name;
        public CrewRole Role;
        public int Level = 1;
        public int PrimaryRoomId = -1;
        public int SecondaryRoomId = -1;
        public int TaskPatientId = -1;
        public int CurrentRoomId = -1;
        public double PositionSlot = -1;
        public double FromSlot = -1;
        public double ToSlot = -1;
        public long MoveStartedTick;
        public long MoveEndsTick;
    }

    [Serializable]
    public sealed class PatientState
    {
        public int Id;
        public string Name;
        public TownId Town;
        public CarePath Path;
        // Stage zero is consultation; stage one is the path's specialist room.
        public int Stage;
        public PatientPhase Phase;
        public int RoomId = -1;
        public int CrewId = -1;
        public int LastRoomId = -1;
        public double FromSlot = -1;
        public double ToSlot = -1;
        public long PhaseStartedTick;
        public long PhaseEndsTick;
        public long ArrivalTick;
        public long WaitingSinceTick;
        public long WaitingTicks;
    }

    [Serializable]
    public sealed class TownProgress
    {
        public TownId Town;
        public int Completed;
        public int Reputation;
        public int ProjectIndex;
        public int ProjectProgress;
        public int CompletedProjects;
        public int ProjectOneProgress;
        public int ProjectTwoProgress;
        public int ProjectCompletionMask;
    }

    public readonly struct CommandResult
    {
        public bool Success { get; }
        public string Message { get; }
        public int Cost { get; }
        public CommandResult(bool success, string message, int cost = 0)
        { Success = success; Message = message; Cost = cost; }
    }

    [Serializable]
    public sealed class AdvanceReport
    {
        public double Seconds;
        public int Completed;
        public int CoinsEarned;
        public int ReputationEarned;
        public int ProjectsCompleted;
        public RoomKind? Bottleneck;
        public int Waiting;
    }
}
