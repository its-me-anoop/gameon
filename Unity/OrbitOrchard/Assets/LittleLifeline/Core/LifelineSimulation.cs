using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LittleLifeline.Core
{
    /// <summary>
    /// Event-driven hospital simulation shared by foreground and offline progress.
    /// State is the complete save contract; no clock, engine API or hidden RNG is used.
    /// </summary>
    public sealed class LifelineSimulation
    {
        private static readonly string[] ResidentNames = { "Robin", "Elliot", "Pip", "Alma", "Kit", "Wren", "Bea", "Theo", "Mina", "Sol", "Fern", "Ash" };
        private const long MaximumAdvanceTicks = 864000;

        public SimulationState State { get; }
        public double ElapsedSeconds => (State.Tick + State.SubTick) / LifelineRules.TicksPerSecond;
        public double TimeRemaining => State.Mode == SimulationMode.Weekly
            ? Math.Max(0, LifelineRules.WeeklySeconds - ElapsedSeconds) : double.PositiveInfinity;
        public long LeaderboardScore => Math.Max(0, (long)State.TotalCompleted * LifelineRules.WeeklyScoreUnit
            - Math.Min(LifelineRules.WeeklyScoreUnit - 1, State.TotalWaitingTicks));
        public RoomKind? Bottleneck
        {
            get
            {
                RoomKind? result = null;
                var largest = 0;
                foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
                {
                    var count = QueueFor(kind);
                    if (count > largest) { largest = count; result = kind; }
                }
                return result;
            }
        }

        public LifelineSimulation(SimulationState state)
        {
            if (!IsValidState(state)) throw new ArgumentException("The hospital save is incomplete or invalid.", nameof(state));
            State = state;
        }

        public static LifelineSimulation CreateCampaign(ulong seed = 42)
        {
            var state = NewState(seed);
            state.Carriages.Add(new CarriageState { Id = 0, Slot = 0, Kind = RoomKind.Consultation });
            for (var id = 0; id < 3; id++) state.Crew.Add(NewCrew(id));
            state.Crew[0].PrimaryRoomId = 0;
            return new LifelineSimulation(state);
        }

        public static LifelineSimulation CreateWeekly(DateTimeOffset now)
        {
            var game = CreateCampaign(LifelineRules.WeeklySeed(now));
            var state = game.State;
            state.Mode = SimulationMode.Weekly;
            state.Town = TownId.Copperhill;
            state.WeeklyId = LifelineRules.WeeklyIdentifier(now);
            state.Coins = 180;
            state.NextArrivalTick = 5;
            state.Carriages.Add(new CarriageState { Id = 1, Slot = 1, Kind = RoomKind.Diagnostics });
            state.Carriages.Add(new CarriageState { Id = 2, Slot = 2, Kind = RoomKind.Recovery });
            state.NextRoomId = 3;
            state.Crew[1].PrimaryRoomId = 1;
            state.Crew[2].PrimaryRoomId = 2;
            return game;
        }

        private static SimulationState NewState(ulong seed)
        {
            var state = new SimulationState { Seed = seed, RandomState = seed };
            foreach (TownId town in Enum.GetValues(typeof(TownId))) state.Towns.Add(new TownProgress { Town = town });
            return state;
        }

        private static CrewState NewCrew(int id) => new CrewState { Id = id, Name = LifelineRules.NextHireName(id), Role = LifelineRules.NextHireRole(id) };

        /// <summary>Advance up to 24 hours per call. Callers account for wall-clock time and offline eligibility.</summary>
        public AdvanceReport Advance(double seconds)
        {
            var report = new AdvanceReport();
            if (State.IsFinished || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return FillReport(report);
            var before = ElapsedSeconds;
            var completed = State.TotalCompleted;
            var coins = State.Coins;
            var reputation = State.Reputation;
            var projects = State.Towns.Sum(t => t.CompletedProjects);
            var fractionalTicks = Math.Min(MaximumAdvanceTicks, seconds * LifelineRules.TicksPerSecond) + State.SubTick;
            var wholeTicks = (long)Math.Floor(fractionalTicks + 1e-9);
            var remainder = Math.Max(0, fractionalTicks - wholeTicks);
            var target = State.Tick + wholeTicks;
            if (State.Mode == SimulationMode.Weekly && target >= LifelineRules.WeeklySeconds * LifelineRules.TicksPerSecond)
            {
                target = LifelineRules.WeeklySeconds * LifelineRules.TicksPerSecond;
                remainder = 0;
            }

            ProcessCurrentTick();
            while (State.Tick < target)
            {
                var next = State.NextArrivalTick;
                foreach (var patient in State.Patients)
                    if (patient.Phase != PatientPhase.Waiting) next = Math.Min(next, patient.PhaseEndsTick);
                if (next > target) { State.Tick = target; UpdateCrewPositions(); break; }
                // All future events have positive duration. This guard also prevents
                // a malformed externally edited state from trapping the simulation.
                if (next <= State.Tick) throw new InvalidOperationException("A hospital event was not advanced.");
                State.Tick = next;
                ProcessCurrentTick();
            }
            State.SubTick = remainder;
            if (State.Mode == SimulationMode.Weekly && State.Tick == LifelineRules.WeeklySeconds * LifelineRules.TicksPerSecond)
            {
                State.IsFinished = true;
                State.SubTick = 0;
            }
            report.Seconds = ElapsedSeconds - before;
            report.Completed = State.TotalCompleted - completed;
            report.CoinsEarned = State.Coins - coins;
            report.ReputationEarned = State.Reputation - reputation;
            report.ProjectsCompleted = State.Towns.Sum(t => t.CompletedProjects) - projects;
            return FillReport(report);
        }

        private AdvanceReport FillReport(AdvanceReport report)
        {
            report.Bottleneck = Bottleneck;
            report.Waiting = State.Patients.Count(p => p.Phase == PatientPhase.Waiting);
            return report;
        }

        public int QueueFor(RoomKind kind) => State.Patients.Count(p => p.Phase == PatientPhase.Waiting && LifelineRules.NeededRoom(p) == kind);

        private void ProcessCurrentTick()
        {
            UpdateCrewPositions();
            // Stable ID order defines simultaneous completions and project rewards.
            State.Patients.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var patient in State.Patients)
            {
                if (patient.Phase == PatientPhase.Waiting || patient.PhaseEndsTick > State.Tick) continue;
                if (patient.Phase == PatientPhase.Walking)
                {
                    var room = Room(patient.RoomId);
                    var crew = Crew(patient.CrewId);
                    patient.Phase = PatientPhase.Treating;
                    patient.PhaseStartedTick = State.Tick;
                    patient.PhaseEndsTick = State.Tick + LifelineRules.TreatmentTicks(room, crew);
                }
                else if (patient.Phase == PatientPhase.Treating) FinishStage(patient);
            }
            State.Patients.RemoveAll(p => p.Phase == PatientPhase.Departing && p.PhaseEndsTick <= State.Tick);
            ApplyPendingRefit();
            ApplyPendingMove();
            if (State.NextArrivalTick <= State.Tick) AdmitNextResident();
            DispatchCrew();
        }

        private void UpdateCrewPositions()
        {
            foreach (var crew in State.Crew)
                if (crew.MoveEndsTick <= State.Tick) crew.PositionSlot = crew.ToSlot;
        }

        private void AdmitNextResident()
        {
            var roll = NextRandom() % 100;
            var path = CarePath.Consultation;
            if (State.Mode == SimulationMode.Weekly || State.ArrivalCount >= 3)
            {
                var consult = State.Town == TownId.Willowbank ? 60 : 30;
                var diagnostic = State.Town == TownId.Copperhill ? 50 : State.Town == TownId.Willowbank ? 25 : 20;
                path = roll < (ulong)consult ? CarePath.Consultation : roll < (ulong)(consult + diagnostic) ? CarePath.Diagnostics : CarePath.Recovery;
            }
            var interval = State.Mode == SimulationMode.Weekly ? 45 : State.Town == TownId.Copperhill ? 75 : 90;
            // Randomness is consumed for every invitation, including a full lounge.
            // No deferred arrival backlog accumulates during absence.
            State.NextArrivalTick = State.Tick + interval + (long)(NextRandom() % 11);
            State.ArrivalCount = Add(State.ArrivalCount, 1);
            if (State.Patients.Count >= LifelineRules.MaxPatients) return;
            // Reserve lounge capacity for short consultations. Missing specialist
            // equipment must never stop all care or lock construction income.
            if (path != CarePath.Consultation &&
                (State.Patients.Count(p => p.Phase == PatientPhase.Waiting) >= LifelineRules.MaxWaiting
                || State.Patients.Count(p => p.Path != CarePath.Consultation && p.Phase != PatientPhase.Departing) >= LifelineRules.MaxWaiting)) return;
            var id = State.NextPatientId++;
            State.Patients.Add(new PatientState
            {
                Id = id, Name = ResidentNames[id % ResidentNames.Length], Town = State.Town, Path = path,
                ArrivalTick = State.Tick, WaitingSinceTick = State.Tick, PhaseStartedTick = State.Tick
            });
        }

        private void DispatchCrew()
        {
            if (State.PendingMoveRoomId >= 0) return;
            foreach (var crew in State.Crew.OrderBy(c => c.Id))
            {
                if (crew.TaskPatientId >= 0) continue;
                CarriageState chosenRoom = null;
                PatientState chosenPatient = null;
                foreach (var roomId in new[] { crew.PrimaryRoomId, crew.SecondaryRoomId })
                {
                    if (roomId < 0 || IsOccupied(roomId) || (State.HasPendingRefit && State.PendingRefitRoomId == roomId)) continue;
                    var room = Room(roomId);
                    foreach (var patient in State.Patients)
                    {
                        if (patient.Phase != PatientPhase.Waiting || LifelineRules.NeededRoom(patient) != room.Kind) continue;
                        if (chosenPatient == null || ComparePatients(patient, chosenPatient) < 0)
                        { chosenRoom = room; chosenPatient = patient; }
                    }
                }
                if (chosenPatient == null) continue;
                BeginCare(chosenPatient, crew, chosenRoom);
            }
        }

        private int ComparePatients(PatientState left, PatientState right)
        {
            if (State.Policy == ServicePolicy.ShortVisitsFirst)
            {
                var work = RemainingWork(left).CompareTo(RemainingWork(right));
                if (work != 0) return work;
            }
            else if (State.Policy == ServicePolicy.ProjectFirst)
            {
                var leftTown = Town(left.Town);
                var rightTown = Town(right.Town);
                var leftMatch = left.Path == LifelineRules.ProjectPath(left.Town, leftTown.ProjectIndex);
                var rightMatch = right.Path == LifelineRules.ProjectPath(right.Town, rightTown.ProjectIndex);
                if (leftMatch != rightMatch) return leftMatch ? -1 : 1;
            }
            var time = left.WaitingSinceTick.CompareTo(right.WaitingSinceTick);
            return time != 0 ? time : left.Id.CompareTo(right.Id);
        }

        private static int RemainingWork(PatientState patient) => (patient.Stage == 0 ? LifelineRules.BaseTreatmentTicks(RoomKind.Consultation) : 0)
            + (patient.Path == CarePath.Consultation ? 0 : LifelineRules.BaseTreatmentTicks((RoomKind)patient.Path));

        private void BeginCare(PatientState patient, CrewState crew, CarriageState room)
        {
            var from = patient.LastRoomId < 0 ? -1 : Room(patient.LastRoomId).Slot;
            var patientWalk = LifelineRules.TransferTicks(from, room.Slot);
            var crewWalk = (long)Math.Abs(crew.PositionSlot - room.Slot) * 8;
            var approach = Math.Max(patientWalk, crewWalk);
            crew.TaskPatientId = patient.Id;
            crew.CurrentRoomId = room.Id;
            crew.FromSlot = crew.PositionSlot;
            crew.ToSlot = room.Slot;
            crew.MoveStartedTick = State.Tick;
            crew.MoveEndsTick = State.Tick + crewWalk;
            patient.RoomId = room.Id;
            patient.CrewId = crew.Id;
            patient.FromSlot = from;
            patient.ToSlot = room.Slot;
            patient.WaitingTicks += State.Tick - patient.WaitingSinceTick + approach;
            patient.Phase = PatientPhase.Walking;
            patient.PhaseStartedTick = State.Tick;
            patient.PhaseEndsTick = State.Tick + approach;
        }

        private void FinishStage(PatientState patient)
        {
            var room = Room(patient.RoomId);
            var crew = Crew(patient.CrewId);
            crew.TaskPatientId = -1;
            crew.PositionSlot = room.Slot;
            patient.LastRoomId = room.Id;
            patient.FromSlot = room.Slot;
            patient.CrewId = -1;
            patient.RoomId = -1;
            patient.PhaseStartedTick = State.Tick;
            if (patient.Stage == 0 && patient.Path != CarePath.Consultation)
            {
                patient.Stage = 1;
                patient.Phase = PatientPhase.Waiting;
                patient.WaitingSinceTick = State.Tick;
                patient.PhaseEndsTick = 0;
                patient.ToSlot = room.Slot;
                return;
            }
            var pay = patient.Path == CarePath.Consultation ? 20 : patient.Path == CarePath.Diagnostics ? 36 : 44;
            var reputation = patient.Path == CarePath.Consultation ? 1 : 2;
            State.Coins = Add(State.Coins, pay);
            State.Reputation = Add(State.Reputation, reputation);
            State.TotalCompleted = Add(State.TotalCompleted, 1);
            State.TotalWaitingTicks = AddLong(State.TotalWaitingTicks, patient.WaitingTicks);
            var town = Town(patient.Town);
            town.Completed = Add(town.Completed, 1);
            town.Reputation = Add(town.Reputation, reputation);
            if (State.Mode == SimulationMode.Campaign) AdvanceProject(town, patient.Path);
            patient.Phase = PatientPhase.Departing;
            patient.ToSlot = -1;
            patient.PhaseEndsTick = State.Tick + 10 + (room.Slot + 1) * 6;
        }

        private void AdvanceProject(TownProgress town, CarePath path)
        {
            var index = town.ProjectIndex;
            if ((town.ProjectCompletionMask & (1 << index)) != 0) return;
            var earned = path == LifelineRules.ProjectPath(town.Town, index) ? 3 : 1;
            var goal = LifelineRules.ProjectGoal(town.Town, index);
            town.ProjectProgress = Math.Min(goal, town.ProjectProgress + earned);
            if (index == 0) town.ProjectOneProgress = town.ProjectProgress;
            else town.ProjectTwoProgress = town.ProjectProgress;
            if (town.ProjectProgress < goal) return;
            town.ProjectCompletionMask |= 1 << index;
            town.CompletedProjects++;
            State.Coins = Add(State.Coins, LifelineRules.ProjectReward(town.Town, index));
        }

        public CommandResult Build(RoomKind kind, int slot)
        {
            if (State.IsFinished) return No("This shift has finished.");
            if (!Defined(kind) || slot < 0 || slot >= LifelineRules.CarriageSlots) return No("Choose one of the four carriage spaces.");
            if (State.Carriages.Any(r => r.Slot == slot)) return No("That space already has a carriage.");
            if (kind != RoomKind.Consultation && State.Mode == SimulationMode.Campaign && State.Reputation < 1) return No("Welcome your first resident to unlock specialist rooms.");
            var cost = LifelineRules.BuildCost(kind);
            if (State.Coins < cost) return No("You need " + cost + " coins for this carriage.");
            State.Coins -= cost;
            State.Carriages.Add(new CarriageState { Id = State.NextRoomId++, Slot = slot, Kind = kind });
            return Yes(LifelineRules.RoomName(kind) + " carriage built. Assign a crew member to open it.", cost);
        }

        public CommandResult Upgrade(int roomId)
        {
            var room = Room(roomId);
            if (State.IsFinished || room == null) return No("Choose an active carriage.");
            if (State.HasPendingRefit && State.PendingRefitRoomId == roomId) return No("This carriage is already preparing for its refit.");
            if (room.Level >= LifelineRules.MaxRoomLevel) return No("This carriage is fully equipped.");
            var cost = LifelineRules.UpgradeCost(room);
            if (State.Coins < cost) return No("You need " + cost + " coins to improve this room.");
            State.Coins -= cost;
            room.Level++;
            return Yes("Equipment improved. New treatments use the faster setup.", cost);
        }

        public CommandResult Assign(int crewId, int primaryRoomId, int secondaryRoomId = -1)
        {
            var crew = Crew(crewId);
            if (State.IsFinished || crew == null) return No("Choose a crew member in this shift.");
            if (primaryRoomId < -1 || secondaryRoomId < -1 || (primaryRoomId >= 0 && Room(primaryRoomId) == null)
                || (secondaryRoomId >= 0 && Room(secondaryRoomId) == null)) return No("Choose rooms on your train.");
            if (primaryRoomId < 0 && secondaryRoomId >= 0) return No("Choose the first room before a second room.");
            crew.PrimaryRoomId = primaryRoomId;
            crew.SecondaryRoomId = secondaryRoomId == primaryRoomId ? -1 : secondaryRoomId;
            return Yes(crew.TaskPatientId >= 0 ? crew.Name + " will change rooms after this resident's care." : crew.Name + " is ready for the new assignment.");
        }

        public CommandResult Reorder(int roomId, int targetSlot)
        {
            var room = Room(roomId);
            if (State.IsFinished || room == null || targetSlot < 0 || targetSlot >= LifelineRules.CarriageSlots) return No("Choose a carriage and a valid space.");
            if (State.PendingMoveRoomId >= 0) return No("The crew is finishing care before the previous move.");
            if (room.Slot == targetSlot) return Yes("This carriage is already in that space.");
            State.PendingMoveRoomId = roomId;
            State.PendingMoveTargetSlot = targetSlot;
            ApplyPendingMove();
            return Yes(State.PendingMoveRoomId >= 0 ? "The crew will finish current treatments, then move the carriages." : "Carriages rearranged. Compare the walking routes.");
        }

        private void ApplyPendingMove()
        {
            if (State.PendingMoveRoomId < 0 || State.Crew.Any(c => c.TaskPatientId >= 0)) return;
            var room = Room(State.PendingMoveRoomId);
            var other = State.Carriages.Find(r => r.Slot == State.PendingMoveTargetSlot);
            var oldSlot = room.Slot;
            room.Slot = State.PendingMoveTargetSlot;
            if (other != null) other.Slot = oldSlot;
            foreach (var crew in State.Crew)
            {
                var slot = crew.CurrentRoomId < 0 ? -1 : Room(crew.CurrentRoomId).Slot;
                crew.PositionSlot = crew.FromSlot = crew.ToSlot = slot;
                crew.MoveStartedTick = crew.MoveEndsTick = State.Tick;
            }
            foreach (var patient in State.Patients)
            {
                if (patient.Phase != PatientPhase.Waiting) continue;
                patient.FromSlot = patient.ToSlot = patient.LastRoomId < 0 ? -1 : Room(patient.LastRoomId).Slot;
            }
            State.PendingMoveRoomId = State.PendingMoveTargetSlot = -1;
        }

        public CommandResult Refit(int roomId, RoomKind kind)
        {
            var room = Room(roomId);
            if (State.IsFinished || room == null || !Defined(kind)) return No("Choose a carriage to refit.");
            if (State.HasPendingRefit) return No("The crew is finishing care before the previous refit.");
            if (room.Kind == kind) return Yes("This carriage already offers that care.");
            if (room.Kind == RoomKind.Consultation && State.Carriages.Count(r => r.Kind == RoomKind.Consultation) == 1)
                return No("Keep at least one consultation carriage to welcome residents.");
            var cost = LifelineRules.RefitCost(kind);
            if (State.Coins < cost) return No("You need " + cost + " coins to refit this carriage.");
            // Reserve construction money at acceptance. Committing a saved request
            // never charges again, and the active resident keeps their original care.
            State.Coins -= cost;
            State.HasPendingRefit = true;
            State.PendingRefitRoomId = roomId;
            State.PendingRefitKind = kind;
            State.PendingRefitCost = cost;
            ApplyPendingRefit();
            return Yes(State.HasPendingRefit ? "Refit booked. This resident will finish care before the carriage changes." : "Carriage refitted. Check its crew assignment.", cost);
        }

        private void ApplyPendingRefit()
        {
            if (!State.HasPendingRefit || IsOccupied(State.PendingRefitRoomId)) return;
            var room = Room(State.PendingRefitRoomId);
            room.Kind = State.PendingRefitKind;
            room.Level = 1;
            State.HasPendingRefit = false;
            State.PendingRefitRoomId = 0;
            State.PendingRefitKind = RoomKind.Consultation;
            State.PendingRefitCost = 0;
        }

        public CommandResult Travel(TownId town)
        {
            if (State.IsFinished || State.Mode == SimulationMode.Weekly) return No("The Weekly Call stays at its shared station.");
            if (!Defined(town)) return No("Choose a town on the route.");
            if (State.Reputation < LifelineRules.TownUnlockReputation(town)) return No("Earn " + LifelineRules.TownUnlockReputation(town) + " reputation to visit " + LifelineRules.TownName(town) + ".");
            if (State.Town == town) return Yes("You are already serving this town.");
            State.Town = town;
            State.NextArrivalTick = State.Tick + 25;
            return Yes("Arrived at " + LifelineRules.TownName(town) + ". Residents already aboard keep their care plans.");
        }

        public CommandResult Hire()
        {
            if (State.IsFinished || State.Mode == SimulationMode.Weekly) return No("The Weekly Call uses the same three crew for everyone.");
            if (State.Crew.Count >= LifelineRules.MaxCrew) return No("All six crew members are aboard.");
            var count = State.Crew.Count;
            if (State.Reputation < LifelineRules.HireReputation(count)) return No("Earn " + LifelineRules.HireReputation(count) + " reputation to welcome the next crew member.");
            var cost = LifelineRules.HireCost(count);
            if (State.Coins < cost) return No("You need " + cost + " coins to hire the next crew member.");
            State.Coins -= cost;
            var crew = NewCrew(count);
            State.Crew.Add(crew);
            return Yes(crew.Name + " has joined. Choose their room assignment.", cost);
        }

        public CommandResult Train(int crewId)
        {
            var crew = Crew(crewId);
            if (State.IsFinished || crew == null) return No("Choose a crew member in this shift.");
            if (crew.Level >= LifelineRules.MaxCrewLevel) return No(crew.Name + " has completed all training.");
            var cost = LifelineRules.TrainCost(crew);
            if (State.Coins < cost) return No("You need " + cost + " coins for this training.");
            State.Coins -= cost;
            crew.Level++;
            return Yes(crew.Name + " is ready to use their training on the next treatment.", cost);
        }

        public CommandResult SetPolicy(ServicePolicy policy)
        {
            if (State.IsFinished || !Defined(policy)) return No("Choose a service policy for this shift.");
            State.Policy = policy;
            return Yes("The next available crew member will follow this policy.");
        }

        public CommandResult ChooseProject(int projectIndex)
        {
            if (State.IsFinished || State.Mode == SimulationMode.Weekly) return No("Community projects belong to your main hospital route.");
            if (projectIndex < 0 || projectIndex > 1) return No("Choose one of this town's two projects.");
            var town = Town(State.Town);
            town.ProjectIndex = projectIndex;
            town.ProjectProgress = projectIndex == 0 ? town.ProjectOneProgress : town.ProjectTwoProgress;
            return Yes("Working toward: " + LifelineRules.ProjectName(town.Town, projectIndex) + ".");
        }

        private CarriageState Room(int id) => State.Carriages.Find(r => r.Id == id);
        private CrewState Crew(int id) => State.Crew.Find(c => c.Id == id);
        private TownProgress Town(TownId town) => State.Towns.Find(t => t.Town == town);
        private bool IsOccupied(int roomId) => State.Patients.Any(p => p.RoomId == roomId && (p.Phase == PatientPhase.Walking || p.Phase == PatientPhase.Treating));
        private static CommandResult No(string message) => new CommandResult(false, message);
        private static CommandResult Yes(string message, int cost = 0) => new CommandResult(true, message, cost);
        private static int Add(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
        private static long AddLong(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
        private static bool Defined<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);

        private ulong NextRandom()
        {
            unchecked
            {
                State.RandomState += 0x9E3779B97F4A7C15ul;
                var value = State.RandomState;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9ul;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBul;
                return value ^ (value >> 31);
            }
        }

        public static bool IsValidState(SimulationState state)
        {
            if (state == null || state.SchemaVersion != 1 || state.RulesVersion != 1 || !Defined(state.Mode) || !Defined(state.Town) || !Defined(state.Policy)) return false;
            if (state.Tick < 0 || state.Tick > long.MaxValue - MaximumAdvanceTicks - 1 || double.IsNaN(state.SubTick) || state.SubTick < 0 || state.SubTick >= 1) return false;
            if (state.Coins < 0 || state.Reputation < 0 || state.TotalCompleted < 0 || state.TotalWaitingTicks < 0 || state.ArrivalCount < 0 || state.NextArrivalTick <= state.Tick || state.NextPatientId < 0) return false;
            if (state.Carriages == null || state.Carriages.Count < 1 || state.Carriages.Count > LifelineRules.CarriageSlots || state.Crew == null || state.Crew.Count < 3 || state.Crew.Count > LifelineRules.MaxCrew
                || state.Patients == null || state.Patients.Count > LifelineRules.MaxPatients || state.Towns == null || state.Towns.Count != 3) return false;
            var roomIds = new HashSet<int>();
            var slots = new HashSet<int>();
            foreach (var room in state.Carriages)
                if (room == null || room.Id < 0 || !roomIds.Add(room.Id) || !slots.Add(room.Slot) || room.Slot < 0 || room.Slot >= LifelineRules.CarriageSlots || !Defined(room.Kind) || room.Level < 1 || room.Level > LifelineRules.MaxRoomLevel) return false;
            if (!state.Carriages.Any(r => r.Kind == RoomKind.Consultation) || state.NextRoomId <= roomIds.Max()) return false;
            if (state.PendingMoveRoomId >= 0)
            {
                if (!roomIds.Contains(state.PendingMoveRoomId) || state.PendingMoveTargetSlot < 0 || state.PendingMoveTargetSlot >= LifelineRules.CarriageSlots) return false;
            }
            else if (state.PendingMoveRoomId != -1 || state.PendingMoveTargetSlot != -1) return false;
            if (state.HasPendingRefit)
            {
                if (!roomIds.Contains(state.PendingRefitRoomId) || !Defined(state.PendingRefitKind)
                    || state.PendingRefitCost != LifelineRules.RefitCost(state.PendingRefitKind)) return false;
                var room = state.Carriages.Find(r => r.Id == state.PendingRefitRoomId);
                if (room.Kind == state.PendingRefitKind || (room.Kind == RoomKind.Consultation
                    && state.Carriages.Count(r => r.Kind == RoomKind.Consultation) == 1)) return false;
            }
            var towns = new HashSet<TownId>();
            foreach (var town in state.Towns)
            {
                if (town == null || !Defined(town.Town) || !towns.Add(town.Town) || town.Completed < 0 || town.Reputation < 0 || town.ProjectIndex < 0 || town.ProjectIndex > 1
                    || town.ProjectOneProgress < 0 || town.ProjectOneProgress > LifelineRules.ProjectGoal(town.Town, 0)
                    || town.ProjectTwoProgress < 0 || town.ProjectTwoProgress > LifelineRules.ProjectGoal(town.Town, 1)
                    || town.ProjectCompletionMask < 0 || town.ProjectCompletionMask > 3
                    || town.CompletedProjects != ((town.ProjectCompletionMask & 1) + ((town.ProjectCompletionMask >> 1) & 1))
                    || town.ProjectProgress != (town.ProjectIndex == 0 ? town.ProjectOneProgress : town.ProjectTwoProgress)) return false;
                var earnedMask = (town.ProjectOneProgress == LifelineRules.ProjectGoal(town.Town, 0) ? 1 : 0)
                    | (town.ProjectTwoProgress == LifelineRules.ProjectGoal(town.Town, 1) ? 2 : 0);
                if (town.ProjectCompletionMask != earnedMask) return false;
            }
            var crewIds = new HashSet<int>();
            var taskIds = new HashSet<int>();
            foreach (var crew in state.Crew)
            {
                if (crew == null || crew.Id < 0 || crew.Id >= LifelineRules.MaxCrew || !crewIds.Add(crew.Id) || string.IsNullOrEmpty(crew.Name) || !Defined(crew.Role)
                    || crew.Level < 1 || crew.Level > LifelineRules.MaxCrewLevel || !RoomReference(crew.PrimaryRoomId, roomIds) || !RoomReference(crew.SecondaryRoomId, roomIds)
                    || !RoomReference(crew.CurrentRoomId, roomIds) || (crew.PrimaryRoomId < 0 && crew.SecondaryRoomId >= 0)
                    || !SlotPosition(crew.PositionSlot) || !SlotPosition(crew.FromSlot) || !SlotPosition(crew.ToSlot)
                    || crew.MoveStartedTick < 0 || crew.MoveStartedTick > state.Tick || crew.MoveEndsTick < crew.MoveStartedTick || crew.TaskPatientId < -1) return false;
                if (crew.TaskPatientId >= 0 && !taskIds.Add(crew.TaskPatientId)) return false;
            }
            for (var id = 0; id < state.Crew.Count; id++) if (!crewIds.Contains(id)) return false;
            var patientIds = new HashSet<int>();
            var occupied = new HashSet<int>();
            foreach (var patient in state.Patients)
            {
                if (patient == null || patient.Id < 0 || patient.Id >= state.NextPatientId || !patientIds.Add(patient.Id) || string.IsNullOrEmpty(patient.Name) || !Defined(patient.Town) || !Defined(patient.Path) || !Defined(patient.Phase)
                    || patient.Stage < 0 || patient.Stage > 1 || (patient.Stage == 1 && patient.Path == CarePath.Consultation)
                    || !RoomReference(patient.LastRoomId, roomIds) || !SlotPosition(patient.FromSlot) || !SlotPosition(patient.ToSlot)
                    || patient.ArrivalTick < 0 || patient.ArrivalTick > state.Tick || patient.WaitingSinceTick < 0 || patient.WaitingSinceTick > state.Tick
                    || patient.WaitingTicks < 0 || patient.PhaseStartedTick < 0 || patient.PhaseStartedTick > state.Tick) return false;
                var busy = patient.Phase == PatientPhase.Walking || patient.Phase == PatientPhase.Treating;
                if (busy)
                {
                    if (!roomIds.Contains(patient.RoomId) || !occupied.Add(patient.RoomId) || !crewIds.Contains(patient.CrewId) || patient.PhaseEndsTick <= state.Tick) return false;
                    var crew = state.Crew.Find(c => c.Id == patient.CrewId);
                    if (crew.TaskPatientId != patient.Id || crew.CurrentRoomId != patient.RoomId) return false;
                    if (state.Carriages.Find(r => r.Id == patient.RoomId).Kind != LifelineRules.NeededRoom(patient)) return false;
                }
                else if (patient.RoomId != -1 || patient.CrewId != -1 || (patient.Phase == PatientPhase.Departing && patient.PhaseEndsTick <= state.Tick)) return false;
            }
            foreach (var taskId in taskIds)
                if (!state.Patients.Any(p => p.Id == taskId && (p.Phase == PatientPhase.Walking || p.Phase == PatientPhase.Treating))) return false;
            if (state.Mode == SimulationMode.Weekly)
            {
                if (state.Tick > LifelineRules.WeeklySeconds * LifelineRules.TicksPerSecond || state.Crew.Count != 3
                    || !DateTime.TryParseExact(state.WeeklyId, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monday) || monday.DayOfWeek != DayOfWeek.Monday
                    || state.IsFinished != (state.Tick == LifelineRules.WeeklySeconds * LifelineRules.TicksPerSecond)) return false;
            }
            else if (state.IsFinished || !string.IsNullOrEmpty(state.WeeklyId)) return false;
            return true;
        }

        private static bool RoomReference(int id, HashSet<int> rooms) => id == -1 || rooms.Contains(id);
        private static bool SlotPosition(double value) => !double.IsNaN(value) && value >= -1 && value < LifelineRules.CarriageSlots;
    }
}
