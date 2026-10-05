using System;
using System.Collections.Generic;

namespace NagaRace.Domain.Race
{
    /// <summary>コースグラフが 04 §2 の制約を満たさない場合に投げる。</summary>
    public sealed class CourseValidationException : Exception
    {
        public CourseValidationException(string courseId, IReadOnlyList<string> errors)
            : base($"コース '{courseId}' が不正です:\n- " + string.Join("\n- ", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }
}
