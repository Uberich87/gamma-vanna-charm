using System;
using Xunit;

namespace GammaVannaCharm.Core.Tests;

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        Assert.True(condition, message);
    }

    public static void False(bool condition, string message)
    {
        Assert.False(condition, message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        Assert.True(object.Equals(expected, actual), message + ": expected " + expected + ", actual " + actual);
    }

    public static void Equal(double expected, double actual, string message)
    {
        Assert.True(Math.Abs(expected - actual) <= 0.0000001, message + ": expected " + expected + ", actual " + actual);
    }
}
