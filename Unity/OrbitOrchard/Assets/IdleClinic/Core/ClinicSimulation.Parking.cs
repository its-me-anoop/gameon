using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        private static bool IsMovingVehicle(ClinicPatientPhase phase)=>phase==ClinicPatientPhase.DrivingToParking||phase==ClinicPatientPhase.DrivingFromParking;
        private static bool IsVehiclePhase(ClinicPatientPhase phase)=>IsMovingVehicle(phase)||phase==ClinicPatientPhase.WaitingToPark||phase==ClinicPatientPhase.WaitingToExit;
        private static bool HasCompletedCare(ClinicPatientPhase phase)=>phase==ClinicPatientPhase.Leaving||phase==ClinicPatientPhase.WaitingToExit||phase==ClinicPatientPhase.DrivingFromParking||phase==ClinicPatientPhase.WalkingToTaxi||phase==ClinicPatientPhase.WaitingForTaxi||phase==ClinicPatientPhase.TaxiPickingUp||phase==ClinicPatientPhase.TaxiDeparting;
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
            long next=long.MaxValue;bool waitingForRoadWindow=false;
            foreach(var patient in State.Patients)
            {
                if(IsMovingVehicle(patient.Phase))
                {
                    long callOpens=patient.PhaseEndsTick-ClinicRules.EarliestCalledPatientCompletionTicks;
                    if(callOpens>State.Tick)next=System.Math.Min(next,callOpens);
                }
                waitingForRoadWindow|=patient.Phase==ClinicPatientPhase.WaitingToExit||ClinicRules.IsDoctors(State)&&patient.Phase==ClinicPatientPhase.WaitingToPark;
                if(ClinicRules.IsDoctors(State)&&TryDoctorsParkingCrossing(patient,out _,out long crossingEnds)&&crossingEnds>State.Tick)
                    next=System.Math.Min(next,crossingEnds);
            }
            if(waitingForRoadWindow)
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
            bool doctors=ClinicRules.IsDoctors(State);
            if(TaxiRoadBusy||!doctors&&State.TaxiRides.Any(r=>r.Phase==ClinicTaxiPhase.WaitingToDepart)||State.Patients.Any(p=>IsMovingVehicle(p.Phase)
                ||!doctors&&p.ParkingBayId>=0&&(p.Phase==ClinicPatientPhase.Arriving||p.Phase==ClinicPatientPhase.Leaving)))return;
            int roadOwner=doctors?OldestDoctorRoadRequestPatientId():-1;
            foreach(var patient in State.Patients.Where(p=>p.Phase==ClinicPatientPhase.WaitingToPark||p.Phase==ClinicPatientPhase.WaitingToExit)
                .OrderBy(p=>p.PhaseStartedTick).ThenBy(p=>p.Id))
            {
                if(doctors&&patient.Id!=roadOwner)continue;
                bool entering=patient.Phase==ClinicPatientPhase.WaitingToPark;
                int duration=entering?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks;
                if(!ParkingMovementHasSafeWindow(entering))continue;
                Phase(patient,entering?ClinicPatientPhase.DrivingToParking:ClinicPatientPhase.DrivingFromParking,patient.FromAnchor,patient.ToAnchor,duration);
                return;
            }
        }
        private bool ParkingMovementHasSafeWindow(bool entering)
        {
            int duration=entering?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks;
            // A departing car reserves the near lane through its complete departure.
            if((!entering||ClinicRules.IsDoctors(State))&&!RoadWindowAvailable(duration))return false;
            long clears=State.Tick+duration;
            if(ClinicRules.IsDoctors(State))
            {
                foreach(var pedestrian in State.Patients)
                {
                    if(TryDoctorsParkingCrossing(pedestrian,out long crossingStarts,out long crossingEnds)
                        &&crossingStarts<clears&&crossingEnds>State.Tick)return false;
                    if(pedestrian.ParkingBayId<0||(pedestrian.Phase!=ClinicPatientPhase.Dispensing&&pedestrian.Phase!=ClinicPatientPhase.WalkingToPharmacy))continue;
                    var futurePath=ClinicDoctorsNavigation.ArrivalPath(pedestrian.ToAnchor,ClinicRules.ParkingPatientAnchor(pedestrian.ParkingBayId));
                    if(TryParkingCrossingDistances(futurePath,out double first,out _,out _))
                    {
                        // A future completion is safe only if its earliest possible
                        // physical crossing follows the reserved vehicle interval.
                        long careEnds=pedestrian.PhaseEndsTick+(pedestrian.Phase==ClinicPatientPhase.WalkingToPharmacy?48:0);
                        long earliestCrossing=careEnds+(long)System.Math.Floor(first*10/ClinicDoctorsNavigation.MetresPerSecond)-5;
                        if(earliestCrossing<clears)return false;
                    }
                }
                return true;
            }
            return !State.Patients.Any(p=>p.ParkingBayId>=0&&(p.Phase==ClinicPatientPhase.Treating&&p.PhaseEndsTick<=clears
                ||p.Phase==ClinicPatientPhase.WalkingToTreatment&&p.PhaseEndsTick+ClinicRules.FastestTreatmentTicks<=clears));
        }
        private static bool TryDoctorsParkingCrossing(ClinicPatientState patient,out long starts,out long ends)
        {
            starts=ends=0;
            if(patient.ParkingBayId<0||(patient.Phase!=ClinicPatientPhase.Arriving&&patient.Phase!=ClinicPatientPhase.Leaving))return false;
            var path=patient.Phase==ClinicPatientPhase.Arriving?patient.ArrivalPath:ClinicDoctorsNavigation.ArrivalPath(patient.FromAnchor,patient.ToAnchor);
            if(!TryParkingCrossingDistances(path,out double first,out double last,out double total))return false;
            long duration=patient.PhaseEndsTick-patient.PhaseStartedTick;
            starts=patient.PhaseStartedTick+(long)System.Math.Floor(duration*first/total)-5;
            ends=patient.PhaseStartedTick+(long)System.Math.Ceiling(duration*last/total)+5;
            return true;
        }
        private static bool TryParkingCrossingDistances(System.Collections.Generic.IList<ClinicMovementPoint> path,out double first,out double last,out double total)
        {
            total=ClinicDoctorsNavigation.PathLength(path);first=-1;last=0;double distance=0;
            if(total<=0)return false;
            for(int i=1;i<path.Count;i++)
            {
                double dx=path[i].X-path[i-1].X,dz=path[i].Z-path[i-1].Z;
                double length=System.Math.Sqrt(dx*dx+dz*dz);
                // Beyond the east parking pavement, the authored route crosses the
                // bays/vehicle aisle. Walking along the pavement or inside the clinic
                // does not reserve the entire car park for the rest of the journey.
                if(System.Math.Min(path[i].X,path[i-1].X)<-14.70f)
                { if(first<0)first=distance;last=distance+length; }
                distance+=length;
            }
            return first>=0;
        }
    }
}
