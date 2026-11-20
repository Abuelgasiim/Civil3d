using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

public interface IRoadwayCheck
{
    CheckCategory Category { get; }

    IEnumerable<DesignCheckResult> Evaluate(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions options);
}
