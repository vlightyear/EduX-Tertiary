using AngleSharp.Dom;
using Google.OrTools.Sat;
using Google.OrTools.Util;
using Newtonsoft.Json;
using SIS.Models.Admin;
using SIS.Models.TimeTabling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SIS.Services.TimeTabling
{
    public class ScheduleGenerator
    {
        private static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        private readonly int numDays = Weekdays.Length;

        public List<AssignedSession> GenerateSchedule(
            List<CourseSession> sessions,
            List<LearningRoom> rooms,
            TimeSlotConfiguration timeSlotConfig,
            Dictionary<int, HashSet<int>> courseToStudents = null)
        {
            // courseToStudents maps CourseId -> HashSet of StudentIds enrolled in that course
            courseToStudents = courseToStudents ?? new Dictionary<int, HashSet<int>>();

            // Parse available periods (per day)
            var periods = JsonConvert.DeserializeObject<List<dynamic>>(timeSlotConfig.PeriodsData);
            int numPeriodsPerDay = periods.Count;
            int numSessions = sessions.Count;

            // Identify virtual rooms (unlimited capacity)
            var virtualRoomIds = rooms.Where(r => r.RoomType?.ToLower() == "virtual")
                                     .Select(r => (long)r.Id)
                                     .ToHashSet();

            var physicalRoomIds = rooms.Where(r => r.RoomType?.ToLower() != "virtual")
                                      .Select(r => (long)r.Id)
                                      .ToHashSet();

            Console.WriteLine($"Found {virtualRoomIds.Count} virtual rooms and {physicalRoomIds.Count} physical rooms");

            // Build student conflict matrix
            int studentConflictCount = 0;
            var conflictPairs = new HashSet<string>();
            for (int i = 0; i < numSessions; i++)
            {
                for (int j = i + 1; j < numSessions; j++)
                {
                    var si = sessions[i];
                    var sj = sessions[j];

                    if (courseToStudents.ContainsKey(si.CourseId) &&
                        courseToStudents.ContainsKey(sj.CourseId))
                    {
                        var studentsI = courseToStudents[si.CourseId];
                        var studentsJ = courseToStudents[sj.CourseId];
                        var sharedStudents = studentsI.Intersect(studentsJ).ToList();

                        if (sharedStudents.Any())
                        {
                            studentConflictCount++;
                            var pairKey = si.CourseId < sj.CourseId
                                ? $"C{si.CourseId}-C{sj.CourseId}"
                                : $"C{sj.CourseId}-C{si.CourseId}";
                            conflictPairs.Add(pairKey);
                        }
                    }
                }
            }
            Console.WriteLine($"Found {studentConflictCount} session pairs with shared students across {conflictPairs.Count} course pairs");

            CpModel model = new CpModel();

            // Decision variables
            IntVar[] sessionRoom = new IntVar[numSessions];
            IntVar[] sessionDay = new IntVar[numSessions];
            IntVar[] sessionStartPeriod = new IntVar[numSessions];
            IntVar[] sessionUsesVirtualRoom = new IntVar[numSessions]; // Track if using virtual room

            for (int i = 0; i < numSessions; i++)
            {
                var s = sessions[i];

                // Allowed rooms (use PossibleRoomIds if available)
                var validRoomIds = (s.PossibleRoomIds?.Count > 0 ? s.PossibleRoomIds :
                                    rooms.Select(r => r.Id)).Select(id => (long)id).ToArray();

                sessionRoom[i] = model.NewIntVarFromDomain(
                    Domain.FromValues(validRoomIds),
                    $"room_{i}"
                );

                sessionDay[i] = model.NewIntVar(0, numDays - 1, $"day_{i}");

                int maxStartPeriod = Math.Max(0, numPeriodsPerDay - s.DurationPeriods);
                sessionStartPeriod[i] = model.NewIntVar(0, maxStartPeriod, $"period_{i}");

                // Boolean indicator: is this session in a virtual room?
                sessionUsesVirtualRoom[i] = model.NewBoolVar($"isVirtual_{i}");

                // Link indicator to room assignment
                if (virtualRoomIds.Count > 0)
                {
                    // If session is assigned to any virtual room, indicator is true
                    var virtualRoomLiterals = new List<ILiteral>();
                    foreach (var vRoomId in virtualRoomIds.Intersect(validRoomIds))
                    {
                        var isThisVRoom = model.NewBoolVar($"room_{i}_is_{vRoomId}");
                        model.Add(sessionRoom[i] == vRoomId).OnlyEnforceIf(isThisVRoom);
                        model.Add(sessionRoom[i] != vRoomId).OnlyEnforceIf(isThisVRoom.Not());
                        virtualRoomLiterals.Add(isThisVRoom);
                    }

                    if (virtualRoomLiterals.Count > 0)
                    {
                        // sessionUsesVirtualRoom is true iff any virtualRoomLiterals is true
                        model.AddBoolOr(virtualRoomLiterals.Append(sessionUsesVirtualRoom[i].Not()).ToArray());
                        foreach (var lit in virtualRoomLiterals)
                        {
                            model.Add(sessionUsesVirtualRoom[i] >= (IntVar)lit);
                        }
                    }
                    else
                    {
                        // No virtual rooms available for this session
                        model.Add(sessionUsesVirtualRoom[i] == 0);
                    }
                }
                else
                {
                    // No virtual rooms in system
                    model.Add(sessionUsesVirtualRoom[i] == 0);
                }
            }

            // Constraint: Lecturer, Student, and Room non-overlap (day-aware)
            for (int i = 0; i < numSessions; i++)
            {
                for (int j = i + 1; j < numSessions; j++)
                {
                    var si = sessions[i];
                    var sj = sessions[j];

                    // 1. Prevent same lecturer overlap on the same day
                    if (si.LecturerId == sj.LecturerId)
                    {
                        AddDayAwareNoOverlap(model, sessionDay[i], sessionStartPeriod[i], si.DurationPeriods,
                                               sessionDay[j], sessionStartPeriod[j], sj.DurationPeriods);
                    }

                    // 2. Prevent student double-booking
                    // Check if any students are enrolled in both courses
                    bool hasSharedStudents = false;
                    if (courseToStudents.ContainsKey(si.CourseId) &&
                        courseToStudents.ContainsKey(sj.CourseId))
                    {
                        var studentsI = courseToStudents[si.CourseId];
                        var studentsJ = courseToStudents[sj.CourseId];
                        hasSharedStudents = studentsI.Overlaps(studentsJ); // HashSet.Overlaps is O(n)
                    }

                    if (hasSharedStudents)
                    {
                        // Students cannot be in two places at once - enforce no overlap
                        AddDayAwareNoOverlap(model, sessionDay[i], sessionStartPeriod[i], si.DurationPeriods,
                                               sessionDay[j], sessionStartPeriod[j], sj.DurationPeriods);
                    }

                    // 3. Prevent same PHYSICAL room overlap on same day
                    // Virtual rooms can overlap, so only enforce for physical rooms
                    IntVar sameRoom = model.NewBoolVar($"sameRoom_{i}_{j}");
                    model.Add(sessionRoom[i] == sessionRoom[j]).OnlyEnforceIf(sameRoom);
                    model.Add(sessionRoom[i] != sessionRoom[j]).OnlyEnforceIf(sameRoom.Not());

                    // bothUsePhysicalRoom: neither uses virtual room
                    IntVar bothUsePhysicalRoom = model.NewBoolVar($"bothPhysical_{i}_{j}");
                    model.AddBoolAnd(new ILiteral[] {
                        sessionUsesVirtualRoom[i].Not(),
                        sessionUsesVirtualRoom[j].Not()
                    }).OnlyEnforceIf(bothUsePhysicalRoom);
                    model.AddBoolOr(new ILiteral[] {
                        sessionUsesVirtualRoom[i],
                        sessionUsesVirtualRoom[j]
                    }).OnlyEnforceIf(bothUsePhysicalRoom.Not());

                    // Only enforce room non-overlap if:
                    // 1. Same room AND
                    // 2. Both sessions use physical rooms (not virtual)
                    AddDayAwareNoOverlap(model,
                        sessionDay[i], sessionStartPeriod[i], si.DurationPeriods,
                        sessionDay[j], sessionStartPeriod[j], sj.DurationPeriods,
                        sameRoom, bothUsePhysicalRoom);
                }
            }

            // === Soft preference: minimize virtual room usage ===
            // Encourage physical rooms when possible
            IntVar totalVirtualUsage = model.NewIntVar(0, numSessions, "totalVirtualUsage");
            model.Add(totalVirtualUsage == LinearExpr.Sum(sessionUsesVirtualRoom));
            model.Minimize(totalVirtualUsage);

            // === Enforce MeetingFrequencyPerWeek per course ===
            var sessionsByCourse = sessions.Select((s, idx) => new { Session = s, Index = idx })
                                           .GroupBy(x => x.Session.CourseId);

            foreach (var courseGroup in sessionsByCourse)
            {
                var indices = courseGroup.Select(x => x.Index).ToArray();
                int requiredMeetings = courseGroup.First().Session.MeetingFrequencyPerWeek;
                if (requiredMeetings <= 0) requiredMeetings = 1;

                // Create dayUsed[d] for this course
                IntVar[] dayUsed = new IntVar[numDays];
                for (int d = 0; d < numDays; d++)
                {
                    dayUsed[d] = model.NewBoolVar($"course_{courseGroup.Key}_dayUsed_{d}");
                }

                // For each day, ensure at most one session per course
                for (int d = 0; d < numDays; d++)
                {
                    var isOnThisDayVars = new List<IntVar>();
                    foreach (var idx in indices)
                    {
                        var isOnDay = model.NewBoolVar($"sess_{idx}_isOnDay_{d}");
                        model.Add(sessionDay[idx] == d).OnlyEnforceIf(isOnDay);
                        model.Add(sessionDay[idx] != d).OnlyEnforceIf(isOnDay.Not());

                        isOnThisDayVars.Add(isOnDay);
                        model.Add(isOnDay <= dayUsed[d]);
                    }

                    if (isOnThisDayVars.Count > 0)
                    {
                        model.Add(dayUsed[d] <= LinearExpr.Sum(isOnThisDayVars));
                        model.Add(LinearExpr.Sum(isOnThisDayVars) <= 1);
                    }
                }

                // Require exact number of distinct days
                model.Add(LinearExpr.Sum(dayUsed) == requiredMeetings);
            }

            // Solve
            CpSolver solver = new CpSolver();
            solver.StringParameters = "max_time_in_seconds:7200;num_search_workers:8";
            CpSolverStatus status = solver.Solve(model);
            Console.WriteLine($"Solver Status: {status}");
            if (status == CpSolverStatus.Feasible || status == CpSolverStatus.Optimal)
            {
                Console.WriteLine($"Virtual room sessions: {solver.ObjectiveValue}");
            }

            var assigned = new List<AssignedSession>();
            if (status == CpSolverStatus.Feasible || status == CpSolverStatus.Optimal)
            {
                for (int i = 0; i < numSessions; i++)
                {
                    assigned.Add(new AssignedSession
                    {
                        Session = sessions[i],
                        AssignedRoomId = (int)solver.Value(sessionRoom[i]),
                        AssignedDay = Weekdays[(int)solver.Value(sessionDay[i])],
                        AssignedPeriod = (int)solver.Value(sessionStartPeriod[i])
                    });
                }
            }
            else
            {
                Console.WriteLine("No feasible schedule found.");
            }

            return assigned;
        }

        // day-aware non-overlap with optional additional condition
        private void AddDayAwareNoOverlap(
            CpModel model,
            IntVar dayA, IntVar startA, int durA,
            IntVar dayB, IntVar startB, int durB,
            IntVar? sameRoom = null,
            IntVar? additionalCondition = null)
        {
            // sameDay bool
            var sameDay = model.NewBoolVar($"sameDay_{startA.Name}_{startB.Name}");
            model.Add(dayA == dayB).OnlyEnforceIf(sameDay);
            model.Add(dayA != dayB).OnlyEnforceIf(sameDay.Not());

            // aBeforeB, bBeforeA
            var aBeforeB = model.NewBoolVar($"aBeforeB_{startA.Name}_{startB.Name}");
            var bBeforeA = model.NewBoolVar($"bBeforeA_{startA.Name}_{startB.Name}");

            model.Add(startA + durA <= startB).OnlyEnforceIf(aBeforeB);
            model.Add(startA + durA > startB).OnlyEnforceIf(aBeforeB.Not());

            model.Add(startB + durB <= startA).OnlyEnforceIf(bBeforeA);
            model.Add(startB + durB > startA).OnlyEnforceIf(bBeforeA.Not());

            // Build disjunction: non-overlap OR not sameDay OR not sameRoom OR not additionalCondition
            var disjuncts = new List<ILiteral> { aBeforeB, bBeforeA, sameDay.Not() };

            if (sameRoom != null)
            {
                disjuncts.Add(sameRoom.Not());
            }

            if (additionalCondition != null)
            {
                disjuncts.Add(additionalCondition.Not());
            }

            model.AddBoolOr(disjuncts.ToArray());
        }
    }
}