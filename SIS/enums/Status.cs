namespace SIS.Enums
{
    public enum Status
    {
        Active = 1,
        Inactive = 2,
        Deleted = 3,
        Pending = 4,
        Approved = 5,
        Rejected = 6,
        Registered = 7,
        Admitted = 8,
        Paid = 9,
        Completed = 10,
        Canceled = 11,
        WaitListed = 12,
        Denied = 13,
        Published = 14,
        Unpublished = 15,
        Unregistered = 16,
        AcademicProbation = 17,
        Excluded = 18,
        Withdrawn = 19,
        PendingProgression = 20,

        // Hostel statuses
        Available = 21,
        Maintenance = 22,
        Reserved = 23,

        // BedSpace statuses
        Occupied = 24,

        // Resource statuses
        Functional = 25,
        NeedsRepair = 26,
        Replaced = 27,

        // Accommodation application statuses
        Submitted = 28,

        // Allocation statuses
        AllocatedPending = 29,
        AllocatedActive = 30,
        AllocatedCompleted = 31,
        AllocatedCancelled = 32,

        // Maintenance request statuses
        InProgress = 34,
        
        // Accommodation period statuses

        Upcoming = 35,
        Closed = 36,
        Scheduled = 37,
        Draft = 38,
        Archived = 39,
        Expired = 40,
        Damaged = 41,

        Graduating = 42,
        Graduated = 43,

        PartiallyPaid = 44
    }

    public static class StatusExtensions
    {
        public static string ToDisplayString(this Status status)
        {
            return status switch
            {
                Status.Active => "ACTIVE",
                Status.Inactive => "INACTIVE",
                Status.Deleted => "DELETED",
                Status.Pending => "PENDING",
                Status.Approved => "APPROVED",
                Status.Rejected => "REJECTED",
                Status.Registered => "REGISTERED",
                Status.Admitted => "ADMITTED",
                Status.Paid => "PAID",
                Status.Completed => "COMPLETED",
                Status.Canceled => "CANCELED",
                Status.WaitListed => "WAITLISTED",
                Status.Denied => "DENIED",
                Status.Published => "PUBLISHED",
                Status.Unpublished => "UNPUBLISHED",
                Status.AcademicProbation => "ACADEMIC PROBATION",
                Status.Excluded => "EXCLUDED",
                Status.Withdrawn => "WITHDRAWN",
                Status.PendingProgression => "PENDING PROGRESSION",
                Status.Unregistered => "UNREGISTERED",

                // Also add these to your StatusExtensions.ToDisplayString method:
                Status.Available => "AVAILABLE",
                Status.Maintenance => "MAINTENANCE",
                Status.Reserved => "RESERVED",
                Status.Occupied => "OCCUPIED",
                Status.Functional => "FUNCTIONAL",
                Status.NeedsRepair => "NEEDS REPAIR",
                Status.Replaced => "REPLACED",
                Status.Submitted => "SUBMITTED",
                Status.AllocatedPending => "ALLOCATED PENDING",
                Status.AllocatedActive => "ALLOCATED ACTIVE",
                Status.AllocatedCompleted => "ALLOCATED COMPLETED",
                Status.AllocatedCancelled => "ALLOCATED CANCELLED",
                Status.InProgress => "IN PROGRESS",
                // Add these new values to your display strings:
                Status.Upcoming => "UPCOMING",
                Status.Closed => "CLOSED",
                Status.Scheduled => "SCHEDULED",
                Status.Archived => "ARCHIVED",

                Status.Draft => "DRAFT",
                _ => status.ToString()
            };
        }
    }
}