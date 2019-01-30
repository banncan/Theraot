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
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//
// Authors:
//		Federico Di Gregorio <fog@initd.org>
//		Jb Evain <jbevain@novell.com>

using NUnit.Framework;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MonoTests.System.Linq.Expressions
{
    [TestFixture]
    public class ExpressionTestAnd
    {
        [Test]
        public void Arg1Null()
        {
            Assert.Throws<ArgumentNullException>(() => { Expression.And(null, Expression.Constant(1)); });
        }

        [Test]
        public void Arg2Null()
        {
            Assert.Throws<ArgumentNullException>(() => { Expression.And(Expression.Constant(1), null); });
        }

        [Test]
        public void NoOperatorClass()
        {
            Assert.Throws<InvalidOperationException>(() => { Expression.And(Expression.Constant(new NoOpClass()), Expression.Constant(new NoOpClass())); });
        }

        [Test]
        public void ArgTypesDifferent()
        {
            Assert.Throws<InvalidOperationException>(() => { Expression.And(Expression.Constant(1), Expression.Constant(true)); });
        }

        [Test]
        public void Double()
        {
            Assert.Throws<InvalidOperationException>(() => { Expression.And(Expression.Constant(1.0), Expression.Constant(2.0)); });
        }

        [Test]
        public void Integer()
        {
            var expr = Expression.And(Expression.Constant(1), Expression.Constant(2));
            Assert.AreEqual(ExpressionType.And, expr.NodeType, "And#01");
            Assert.AreEqual(typeof(int), expr.Type, "And#02");
            Assert.IsNull(expr.Method, "And#03");
            Assert.AreEqual("(1 & 2)", expr.ToString(), "And#04");
        }

        [Test]
        public void Boolean()
        {
            var expr = Expression.And(Expression.Constant(true), Expression.Constant(false));
            Assert.AreEqual(ExpressionType.And, expr.NodeType, "And#05");
            Assert.AreEqual(typeof(bool), expr.Type, "And#06");
            Assert.IsNull(expr.Method, "And#07");
            Assert.AreEqual("(True And False)", expr.ToString(), "And#08");
        }

        [Test]
        public void UserDefinedClass()
        {
            // We can use the simplest version of GetMethod because we already know only one
            // exists in the very simple class we're using for the tests.
            var mi = typeof(OpClass).GetMethod("op_BitwiseAnd");

            var expr = Expression.And(Expression.Constant(new OpClass()), Expression.Constant(new OpClass()));
            Assert.AreEqual(ExpressionType.And, expr.NodeType, "And#09");
            Assert.AreEqual(typeof(OpClass), expr.Type, "And#10");
            Assert.AreEqual(mi, expr.Method, "And#11");
            Assert.AreEqual("op_BitwiseAnd", expr.Method.Name, "And#12");
            Assert.AreEqual("(value(MonoTests.System.Linq.Expressions.OpClass) & value(MonoTests.System.Linq.Expressions.OpClass))",
                expr.ToString(), "And#13");
        }

        [Test]
        public void AndBoolTest()
        {
            var a = Expression.Parameter(typeof(bool), "a");
            var b = Expression.Parameter(typeof(bool), "b");
            var l = Expression.Lambda<Func<bool, bool, bool>>(
                Expression.And(a, b), a, b);

            var be = l.Body as BinaryExpression;
            Assert.IsNotNull(be);
            Assert.AreEqual(typeof(bool), be.Type);
            Assert.IsFalse(be.IsLifted);
            Assert.IsFalse(be.IsLiftedToNull);

            var c = l.Compile();

            Assert.AreEqual(true, c(true, true), "t1");
            Assert.AreEqual(false, c(true, false), "t2");
            Assert.AreEqual(false, c(false, true), "t3");
            Assert.AreEqual(false, c(false, false), "t4");
        }

        [Test]
        public void AndLifted()
        {
            var b = Expression.And(
                Expression.Constant(null, typeof(bool?)),
                Expression.Constant(null, typeof(bool?)));

            Assert.AreEqual(typeof(bool?), b.Type);
            Assert.IsTrue(b.IsLifted);
            Assert.IsTrue(b.IsLiftedToNull);
        }

        [Test]
        public void AndBoolNullableTest()
        {
            var a = Expression.Parameter(typeof(bool?), "a");
            var b = Expression.Parameter(typeof(bool?), "b");
            var l = Expression.Lambda<Func<bool?, bool?, bool?>>(
                Expression.And(a, b), a, b);

            var be = l.Body as BinaryExpression;
            Assert.IsNotNull(be);
            Assert.AreEqual(typeof(bool?), be.Type);
            Assert.IsTrue(be.IsLifted);
            Assert.IsTrue(be.IsLiftedToNull);

            var c = l.Compile();

            Assert.AreEqual(true, c(true, true), "a1");
            Assert.AreEqual(false, c(true, false), "a2");
            Assert.AreEqual(false, c(false, true), "a3");
            Assert.AreEqual(false, c(false, false), "a4");

            Assert.AreEqual(null, c(true, null), "a5");
            Assert.AreEqual(false, c(false, null), "a6");
            Assert.AreEqual(false, c(null, false), "a7");
            Assert.AreEqual(null, c(true, null), "a8");
            Assert.AreEqual(null, c(null, null), "a9");
        }

        [Test]
        public void AndBoolItem()
        {
            var i = Expression.Parameter(typeof(Item<bool>), "i");
            var and = Expression.Lambda<Func<Item<bool>, bool>>(
                Expression.And(
                    Expression.Property(i, "Left"),
                    Expression.Property(i, "Right")), i).Compile();

            var item = new Item<bool>(false, true);
            Assert.AreEqual(false, and(item));
            Assert.IsTrue(item.LeftCalled);
            Assert.IsTrue(item.RightCalled);
        }

        [Test]
        public void AndNullableBoolItem()
        {
            var i = Expression.Parameter(typeof(Item<bool?>), "i");
            var and = Expression.Lambda<Func<Item<bool?>, bool?>>(
                Expression.And(
                    Expression.Property(i, "Left"),
                    Expression.Property(i, "Right")), i).Compile();

            var item = new Item<bool?>(false, true);
            Assert.AreEqual((bool?)false, and(item));
            Assert.IsTrue(item.LeftCalled);
            Assert.IsTrue(item.RightCalled);
        }

        [Test]
        public void AndIntTest()
        {
            var a = Expression.Parameter(typeof(int), "a");
            var b = Expression.Parameter(typeof(int), "b");
            var and = Expression.Lambda<Func<int, int, int>>(
                Expression.And(a, b), a, b).Compile();

            Assert.AreEqual(0, and(0, 0), "t1");
            Assert.AreEqual(0, and(0, 1), "t2");
            Assert.AreEqual(0, and(1, 0), "t3");
            Assert.AreEqual(1, and(1, 1), "t4");
        }

        [Test]
        public void AndIntNullableTest()
        {
            var a = Expression.Parameter(typeof(int?), "a");
            var b = Expression.Parameter(typeof(int?), "b");
            var c = Expression.Lambda<Func<int?, int?, int?>>(
                Expression.And(a, b), a, b).Compile();

            Assert.AreEqual((int?)1, c(1, 1), "a1");
            Assert.AreEqual((int?)0, c(1, 0), "a2");
            Assert.AreEqual((int?)0, c(0, 1), "a3");
            Assert.AreEqual((int?)0, c(0, 0), "a4");

            Assert.AreEqual(null, c(1, null), "a5");
            Assert.AreEqual(null, c(0, null), "a6");
            Assert.AreEqual(null, c(null, 0), "a7");
            Assert.AreEqual(null, c(1, null), "a8");
            Assert.AreEqual(null, c(null, null), "a9");
        }
    }
}