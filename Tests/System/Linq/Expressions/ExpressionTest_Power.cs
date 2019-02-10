﻿#if LESSTHAN_NET35
extern alias nunitlinq;
#endif

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// Ad
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//
// Authors:
//		Federico Di Gregorio <fog@initd.org>
//		Miguel de Icaza <miguel@novell.com>

using System;
using System.Linq.Expressions;
using NUnit.Framework;

namespace MonoTests.System.Linq.Expressions
{
    [TestFixture]
    public class ExpressionTestPower
    {
        [Test]
        public void Arg1Null()
        {
            Assert.Throws<ArgumentNullException>(() => Expression.Power(null, Expression.Constant(1.0)));
        }

        [Test]
        public void Arg2Null()
        {
            Assert.Throws<ArgumentNullException>(() => Expression.Power(Expression.Constant(1.0), null));
        }

        [Test]
        public void ArgTypesDifferent()
        {
            Assert.Throws<InvalidOperationException>(() => Expression.Power(Expression.Constant(1), Expression.Constant(2.0)));
        }

        [Test]
        public void ArgTypesDouble()
        {
            var p = Expression.Power(Expression.Constant(1.0), Expression.Constant(2.0));

            Assert.AreEqual(ExpressionType.Power, p.NodeType, "Power#01");
            Assert.AreEqual(typeof(double), p.Type, "Add#02");
#if TARGETS_NETCORE
            // This changed for .NET Core
            Assert.AreEqual("(1 ** 2)", p.ToString());
#else
            Assert.AreEqual("(1 ^ 2)", p.ToString());
#endif
        }

        [Test]
        public void ArgTypesFloat()
        {
            Assert.Throws<InvalidOperationException>(() => Expression.Power(Expression.Constant((float)1), Expression.Constant((float)2)));
        }

        [Test]
        public void ArgTypesInt()
        {
            Assert.Throws<InvalidOperationException>(() => Expression.Power(Expression.Constant(1), Expression.Constant(2)));
        }

        [Test]
        public void NullablePower()
        {
            var a = Expression.Parameter(typeof(double?), "a");
            var b = Expression.Parameter(typeof(double?), "b");

            var power = Expression.Lambda<Func<double?, double?, double?>>
            (
                Expression.Power(a, b), a, b
            ).Compile();

            Assert.AreEqual((double?)1, power(1, 10));
            Assert.AreEqual((double?)16, power(2, 4));
            Assert.AreEqual(null, power(1, null));
            Assert.AreEqual(null, power(null, 1));
            Assert.AreEqual(null, power(null, null));
        }

        [Test]
        public void TestCompile()
        {
            var a = Expression.Parameter(typeof(double), "a");
            var b = Expression.Parameter(typeof(double), "b");

            var power = Expression.Lambda<Func<double, double, double>>
            (
                Expression.Power(a, b), a, b
            ).Compile();

            Assert.AreEqual(1, power(1, 10));
            Assert.AreEqual(16, power(2, 4));
        }
    }
}