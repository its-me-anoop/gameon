using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        private static bool IsMovingVehicle(ClinicPatientPhase phase)=>phase==ClinicPatientPhase.DrivingToParking||phase==ClinicPatientPhase.DrivingFromParking;
        private static bool IsVehiclePhase(ClinicPatientPhase phase)=>IsMovingVehicle(phase)||phase==ClinicPatientPhase.WaitingToPark||phase==ClinicPatientPhase.WaitingToExit;
        private static bool HasCompletedCare(ClinicPatientPhase phase)=>phase==ClinicPatientPhase.Leaving||phase==ClinicPatientPhase.WaitingToExit||phase==ClinicPatientPhase.DrivingFromParking;
        private bool CanCallPatientDuringVehicleMovement(ClinicPatientState patient)
        {
            if(patient.ParkingBayId<0)return true;
            var moving=State.Patients.FirstOrDefault(p=>IsMovingVehicle(p.Phase));
            // Even fully upgraded care cannot finish before the shared aisle clears.
            // Other visitors remain eligible, so transport never stops the clinic.
            return moving==null||moving.PhaseEndsTick-State.Tick<=ClinicRules.EarliestCalledPatientCompletionTicks;
        }
        private long NextParkingEligibilityTick()
        {
            long next=long.MaxValue;bool waitingToExit=false;
            foreach(var patient in State.Patients)
            {
                if(IsMovingVehicle(patient.Phase))
                {
                    long callOpens=patient.PhaseEndsTick-ClinicRules.EarliestCalledPatientCompletionTicks;
                    if(callOpens>State.Tick)next=System.Math.Min(next,callOpens);
                }
                waitingToExit|=patient.Phase==ClinicPatientPhase.WaitingToExit;
            }
            if(waitingToExit)
            {
                // This eligibility boundary must be an event: a coarse/offline step
                // must resume departing cars at the same tick as a rendered frame.
                long delay=ClinicRules.StreetCrossingEndsTick-ClinicRules.TrafficTick(State)%ClinicRules.StreetCrossingCycleTicks;
                if(delay<=0)delay+=ClinicRules.StreetCrossingCycleTicks;
                next=System.Math.Min(next,State.Tick+delay);
            }
            return next;
        }
        private void DispatchParkingVehicles()
        {
            if(State.Patients.Any(p=>IsMovingVehicle(p.Phase)||p.ParkingBayId>=0&&(p.Phase==ClinicPatientPhase.Arriving||p.Phase==ClinicPatientPhase.Leaving)))return;
            foreach(var patient in State.Patients.Where(p=>p.Phase==ClinicPatientPhase.WaitingToPark||p.Phase==ClinicPatientPhase.WaitingToExit)
                .OrderBy(p=>p.PhaseStartedTick).ThenBy(p=>p.Id))
            {
                bool entering=patient.Phase==ClinicPatientPhase.WaitingToPark;
                int duration=entering?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks;
                if(!entering)
                {
                    // Reserve the near road lane for the whole departure. Street pedestrians
                    // cross on the same simulation clock, including after background/resume.
                    long cycle=ClinicRules.TrafficTick(State)%ClinicRules.StreetCrossingCycleTicks;
                    long nextCrossing=cycle<ClinicRules.StreetCrossingStartsTick?ClinicRules.StreetCrossingStartsTick-cycle:
                        ClinicRules.StreetCrossingCycleTicks+ClinicRules.StreetCrossingStartsTick-cycle;
                    if(cycle>=ClinicRules.StreetCrossingStartsTick&&cycle<ClinicRules.StreetCrossingEndsTick||duration>=nextCrossing)continue;
                }
                long clears=State.Tick+duration;
                bool crossingDue=State.Patients.Any(p=>p.ParkingBayId>=0&&(p.Phase==ClinicPatientPhase.Treating&&p.PhaseEndsTick<=clears
                    ||p.Phase==ClinicPatientPhase.WalkingToTreatment&&p.PhaseEndsTick+ClinicRules.FastestTreatmentTicks<=clears));
                if(crossingDue)continue;
                Phase(patient,entering?ClinicPatientPhase.DrivingToParking:ClinicPatientPhase.DrivingFromParking,patient.FromAnchor,patient.ToAnchor,duration);
                return;
            }
        }
    }
}
