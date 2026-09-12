using System;
using System.Collections.Generic;


namespace IdleClinic.Core
{
    /// <summary>Fixed directed aisles join each room through its real aperture. Work sockets never move.</summary>
    public static class ClinicDoctorsNavigation
    {
        public readonly struct Point
        {
            public readonly float x, z;
            public Point(float x, float z) { this.x = x; this.z = z; }
        }
        public const double MetresPerSecond = 1.7;
        public static Point Anchor(string anchor)
        {
            if (anchor == "entrance") return new Point(.38f, -10.90f);
            if (anchor == "exit") return new Point(-.38f, -10.90f);
            if (string.IsNullOrEmpty(anchor)) throw new ArgumentException("A doctors clinic movement anchor is required.", nameof(anchor));
            int id = Index(anchor);
            bool staff = anchor.EndsWith(".staff", StringComparison.Ordinal), patient = anchor.EndsWith(".patient", StringComparison.Ordinal);
            if (anchor.StartsWith("reception.desk.", StringComparison.Ordinal) && id >= 0 && id < 4 && (staff || patient))
                return new Point(-10.72f + id * 2.60f, -5.45f + (staff ? .70f : -.89f));
            if (anchor.StartsWith("firstaid.station.", StringComparison.Ordinal) && id >= 0 && id < 4 && (staff || patient))
                return new Point(-10.72f + id * 2.60f + (staff ? .83f : 0), .86f + (staff ? -.06f : 0));
            if (anchor.StartsWith("consultation.station.", StringComparison.Ordinal) && id >= 0 && id < 4 && (staff || patient))
                return new Point(-9.4f + id * 5.6f + (staff ? .28f : -.12f), 7.05f + (staff ? .95f : -1f));
            if (anchor.StartsWith("pharmacy.station.", StringComparison.Ordinal) && id >= 0 && id < 2 && (staff || patient))
                return new Point(4.05f + id * 3.50f, 1.20f + (staff ? .70f : -.89f));
            if (anchor.StartsWith("reception.queue.", StringComparison.Ordinal) && id >= 0 && id < 23)
                return new Point(-11.25f + (id % 8) * 1.10f, -8.25f - (id / 8) * .70f);
            if (anchor.StartsWith("waiting.seat.", StringComparison.Ordinal) && id >= 0 && id < 30)
                return new Point(2.2f + (id % 6 / 2) * 2.8f + (id % 2) * 1.8f, -6f + id / 6);
            if (anchor.StartsWith("firstaid.standing.", StringComparison.Ordinal) && id >= 0 && id < 4)
                return new Point(-2.2f, -1.05f + id * .72f);
            if (anchor.StartsWith("parking.bay.", StringComparison.Ordinal) && id >= 0 && id < 12 && patient)
                return new Point(id % 2 == 0 ? -21f : -16.2f, -7.84f + (id / 2) * 2.9f);
            if (anchor.StartsWith("taxi.dock.", StringComparison.Ordinal) && id >= 0 && id < 2 && patient)
                return new Point(18f + id * 3.6f, -9.8f);
            if (anchor.StartsWith("waiting.toilet.", StringComparison.Ordinal) && id >= 0 && id < 2 && patient)
                return new Point(11.35f + id * 1.75f, -3.70f);
            if (anchor == "waiting.vending.patient") return new Point(9.05f, -1.82f);
            throw new ArgumentException("Unknown doctors clinic movement anchor: " + anchor, nameof(anchor));
        }
        public static double PathLength(string from, string to, bool staff = false)
        {
            var previous = Anchor(from); double distance = 0;
            Build(from, to, staff, previous, point =>
            {
                double dx = point.x - previous.x, dz = point.z - previous.z;
                distance += Math.Sqrt(dx * dx + dz * dz); previous = point;
            });
            return distance;
        }
        public static int WalkTicks(string from, string to, bool staff = false)
            => Math.Max(1, (int)Math.Ceiling(PathLength(from, to, staff) * ClinicRules.TicksPerSecond / MetresPerSecond));
        public static List<ClinicMovementPoint> ArrivalPath(string from, string to)
        {
            var start = Anchor(from);
            var path = new List<ClinicMovementPoint> { new ClinicMovementPoint(start.x, start.z) };
            Build(from, to, false, start, point => path.Add(new ClinicMovementPoint(point.x, point.z)));
            return path;
        }
        public static double PathLength(IList<ClinicMovementPoint> path)
        {
            double distance = 0;
            for (int i = 1; i < path.Count; i++) distance += Distance(path[i - 1], path[i]);
            return distance;
        }
        public static int WalkTicks(IList<ClinicMovementPoint> path)
            => Math.Max(1, (int)Math.Ceiling(PathLength(path) * ClinicRules.TicksPerSecond / MetresPerSecond));
        public static ClinicMovementPoint SampleArrivalPath(IList<ClinicMovementPoint> path, double progress)
        {
            int segment;
            return SampleArrivalPath(path, progress, out segment);
        }
        private static ClinicMovementPoint SampleArrivalPath(IList<ClinicMovementPoint> path, double progress, out int segment)
        {
            if (path == null || path.Count < 2) throw new ArgumentException("An arrival path requires its endpoints.", nameof(path));
            double remaining = PathLength(path) * Math.Max(0, Math.Min(1, progress));
            for (segment = 0; segment < path.Count - 1; segment++)
            {
                double distance = Distance(path[segment], path[segment + 1]);
                if (remaining <= distance && distance > .0000001)
                {
                    double fraction = remaining / distance;
                    return new ClinicMovementPoint((float)(path[segment].X + (path[segment + 1].X - path[segment].X) * fraction),
                        (float)(path[segment].Z + (path[segment + 1].Z - path[segment].Z) * fraction));
                }
                remaining -= distance;
            }
            segment = path.Count - 2;
            return new ClinicMovementPoint(path[path.Count - 1].X, path[path.Count - 1].Z);
        }
        public static List<ClinicMovementPoint> RetargetArrivalPath(IList<ClinicMovementPoint> path, double progress, string target)
        {
            var position = SampleArrivalPath(path, progress, out var segment);
            var result = new List<ClinicMovementPoint> { position };
            // Keep only untravelled corridor waypoints. The final point is the old queue slot;
            // replacing it never sends a patient back to the original bay or taxi stand.
            for (int i = segment + 1; i < path.Count - 1; i++)
                result.Add(new ClinicMovementPoint(path[i].X, path[i].Z));
            var destination = Anchor(target);
            result.Add(new ClinicMovementPoint(destination.x, destination.z));
            return result;
        }
        private static double Distance(ClinicMovementPoint a, ClinicMovementPoint b)
        { double x = b.X - a.X, z = b.Z - a.Z; return Math.Sqrt(x * x + z * z); }
        public static void Build(string from,string to,bool staff,Point start,Action<Point> add)
        {
            Point end=Anchor(to);
            bool F(string prefix)=>from!=null&&from.StartsWith(prefix,StringComparison.Ordinal);
            bool T(string prefix)=>to!=null&&to.StartsWith(prefix,StringComparison.Ordinal);
            bool withinWaiting=F("waiting.")&&T("waiting.");
            float fz=Junction(from,staff),tz=Junction(to,staff);float lane=tz>=fz?.38f:-.38f;
            void P(float x,float z)=>add(new Point(x,z));
            // Parking passengers use the outside pavement, never cross occupied parking bays diagonally.
            if(F("parking.bay.")){float crossing=Anchor(from).z-.58f;P(start.x,crossing);P(-14.65f,crossing);P(-14.65f,-10.90f);P(lane,-10.90f);}
            else if(F("taxi.dock.")){P(start.x,-9.80f);P(14.85f,-9.80f);P(14.85f,-10.90f);P(lane,-10.90f);}
            else if(F("reception.desk.")){P(start.x,staff?-3.90f:-7.29f);P(lane,staff?-3.90f:-7.29f);}
            else if(F("firstaid.station.")){P(start.x,-1.06f);P(lane,-1.06f);}
            else if(F("consultation.station."))
            {
                int id=Index(from);float cx=-9.4f+id*5.6f;
                if(staff){P(cx+1.85f,start.z);P(cx+1.85f,5.3f);}
                P(cx-.36f,5.2f);P(cx-.36f,3.40f);P(lane,3.40f);
            }
            else if(F("pharmacy.station.")){P(start.x,staff?2.05f:.34f);P(2.0f,staff?2.05f:.34f);P(2.0f,1.19f);P(lane,1.19f);}
            else if(F("waiting.seat.")){float aisle=SeatAisle(Index(from))-.31f;P(aisle,start.z);P(aisle,-6.68f);if(!withinWaiting)P(lane,-6.68f);}
            else if(F("waiting.toilet.")){P(start.x,-6.20f);P(10.9f,-6.20f);P(10.9f,-6.72f);P(8.7f,-6.72f);if(!withinWaiting)P(lane,-6.68f);}
            else if(F("waiting.vending.")){P(9.05f,-1.95f);P(8.7f,-1.95f);P(8.7f,-6.68f);if(!withinWaiting)P(lane,-6.68f);}
            else if(F("reception.queue."))
            {P(lane,start.z);if(start.z<-10.30f)P(lane,-9.65f);}
            else P(lane,start.z);
            if(T("parking.bay.")){P(lane,-10.90f);P(-14.65f,-10.90f);P(-14.65f,end.z-.58f);P(end.x,end.z-.58f);}
            else if(T("taxi.dock.")){P(lane,-10.90f);P(14.85f,-10.90f);P(14.85f,-9.80f);P(end.x,-9.80f);}
            else if(T("reception.desk.")){P(lane,staff?-3.90f:-6.61f);P(end.x,staff?-3.90f:-6.61f);}
            else if(T("firstaid.station.")){P(lane,-.34f);P(end.x,-.34f);}
            else if(T("consultation.station."))
            {
                float cx=-9.4f+Index(to)*5.6f;P(lane,4.10f);P(cx+.36f,4.10f);P(cx+.36f,5.20f);
                if(staff){P(cx+1.85f,5.20f);P(cx+1.85f,end.z);}
            }
            else if(T("pharmacy.station.")){P(lane,.51f);P(2.0f,.51f);P(2.0f,staff?2.05f:.34f);P(end.x,staff?2.05f:.34f);}
            else if(T("waiting.seat.")){float aisle=SeatAisle(Index(to))+.31f;if(!withinWaiting)P(lane,-7.38f);P(aisle,-7.38f);P(aisle,end.z);}
            else if(T("waiting.toilet.")){if(!withinWaiting)P(lane,-7.38f);P(10.9f,-7.38f);P(10.9f,-6.20f);P(end.x,-6.20f);}
            else if(T("waiting.vending.")){if(!withinWaiting)P(lane,-7.38f);P(8.7f,-7.38f);P(8.7f,-1.95f);P(9.05f,-1.95f);}
            else P(lane,end.z);
            add(end);
        }
        private static float Junction(string anchor,bool staff)
        {
            if(string.IsNullOrEmpty(anchor))return -9.65f;
            if(anchor.StartsWith("consultation.station.",StringComparison.Ordinal))return 4f;
            if(anchor.StartsWith("pharmacy.station.",StringComparison.Ordinal))return .85f;
            if(anchor.StartsWith("firstaid.station.",StringComparison.Ordinal))return -.70f;
            if(anchor.StartsWith("reception.desk.",StringComparison.Ordinal))return staff?-3.90f:-7.05f;
            if(anchor.StartsWith("waiting.",StringComparison.Ordinal))return -7f;
            if(anchor.StartsWith("parking.",StringComparison.Ordinal)||anchor.StartsWith("taxi.",StringComparison.Ordinal))return -10.90f;
            return Anchor(anchor).z;
        }
        private static float SeatAisle(int id)=>3.1f+(id%6/2)*2.8f;
        public static int Index(string anchor)
        {foreach(string p in anchor.Split('.'))if(int.TryParse(p,out int index))return index;return -1;}
    }
}
