// <copyright file="TypeHelperTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.Persistence;

/// <summary>
/// Tests for the <see cref="TypeHelper"/>.
/// </summary>
[TestFixture]
public class TypeHelperTest
{
    /// <summary>
    /// Many concurrent first lookups of the same type don't fail and agree.
    /// </summary>
    [Test]
    public void ConcurrentFirstLookupsAgree()
    {
        var origin = typeof(TypeHelperTest).Assembly;
        var results = new Type[64];

        // The base type is outside of the origin and only this test derives from it, so the lookups are the first ones.
        Parallel.For(0, results.Length, i => results[i] = origin.GetPersistentTypeOf(typeof(System.Collections.ObjectModel.ReadOnlyDictionary<Guid, TypeHelperTest>)));

        Assert.That(results, Is.All.EqualTo(typeof(LookupPersistent)));
    }

    /// <summary>
    /// The "persistent type" of the base type of the test.
    /// </summary>
    public class LookupPersistent : System.Collections.ObjectModel.ReadOnlyDictionary<Guid, TypeHelperTest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LookupPersistent"/> class.
        /// </summary>
        public LookupPersistent()
            : base(new Dictionary<Guid, TypeHelperTest>())
        {
        }
    }
}
