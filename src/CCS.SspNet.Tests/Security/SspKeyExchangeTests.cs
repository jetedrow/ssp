using CCS.SspNet.Protocol;
using CCS.SspNet.Security;
using FluentAssertions;
using System;
using System.Numerics;
using Xunit;

namespace CCS.SspNet.Tests.Security
{
    /// <summary>
    /// The Diffie-Hellman exchange that agrees the half of the key which is different every
    /// session.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspKeyExchangeTests
    {
        [Fact]
        public void BothSidesArriveAtTheSameKeyWithoutItCrossingTheWire()
        {
            var parameters = SspKeyExchangeParameters.Generate(32);

            var host = SspKeyExchange.Create(parameters);

            // What a device does with what it is sent.
            const ulong deviceSecret = 1234567;
            var deviceIntermediate = (ulong)BigInteger.ModPow(parameters.Generator, deviceSecret, parameters.Modulus);
            var deviceKey = (ulong)BigInteger.ModPow(host.HostIntermediate, deviceSecret, parameters.Modulus);

            host.CreateSharedSecret(deviceIntermediate).Should().Be(deviceKey);
        }

        /// <summary>
        /// Unusually for Diffie-Hellman, eSSP wants the generator to be the larger of the two
        /// primes.  The implementation guide says so in one sentence and a device simply will not
        /// agree a key otherwise, so it is worth holding in a test rather than in someone's memory.
        /// </summary>
        [Fact]
        public void TheGeneratedGeneratorIsAlwaysLargerThanTheModulus()
        {
            for (var i = 0; i < 20; i++)
            {
                var parameters = SspKeyExchangeParameters.Generate(24);
                parameters.Generator.Should().BeGreaterThan(parameters.Modulus);
            }
        }

        [Fact]
        public void BothGeneratedNumbersArePrimeBecauseADeviceChecks()
        {
            for (var i = 0; i < 10; i++)
            {
                var parameters = SspKeyExchangeParameters.Generate(28);

                SspPrimes.IsPrime(parameters.Generator).Should().BeTrue();
                SspPrimes.IsPrime(parameters.Modulus).Should().BeTrue();
            }
        }

        [Fact]
        public void ParametersRefuseANumberThatIsNotPrime()
        {
            new Func<object>(() => new SspKeyExchangeParameters(1287821 * 2, 1287821))
                .Should().Throw<ArgumentException>().WithMessage("*not prime*");
        }

        [Fact]
        public void ParametersRefuseAGeneratorSmallerThanTheModulus()
        {
            new Func<object>(() => new SspKeyExchangeParameters(1287821, 982451653))
                .Should().Throw<ArgumentException>().WithMessage("*larger than the modulus*");
        }

        [Theory]
        [InlineData(982451653UL, new byte[] { 0xC5, 0x05, 0x8F, 0x3A, 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(1287821UL, new byte[] { 0x8D, 0xA6, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(7554354432121UL, new byte[] { 0x79, 0xC8, 0x9C, 0xE2, 0xDE, 0x06, 0x00, 0x00 })]
        public void TheExchangeNumbersGoOutAsTheManualPrintsThem(ulong value, byte[] expected)
        {
            SspValues.WriteUInt64(value).Should().Equal(expected);
            SspValues.ReadUInt64(expected).Should().Be(value);
        }

        /// <summary>
        /// The manufacturer's half comes first in the key, and a device leaves the factory
        /// expecting one particular value for it.
        /// </summary>
        [Fact]
        public void TheDefaultKeyIsTheBytesADeviceShipsWith()
        {
            SspEncryptionKey.Default.ToArray().Should()
                .StartWith(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x01, 0x23, 0x45, 0x67 })
                .And.HaveCount(16);
        }

        [Fact]
        public void TheNegotiatedHalfGoesAfterTheFixedOne()
        {
            var key = new SspEncryptionKey(0, 0x0807060504030201);

            key.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Fact]
        public void AKeyDoesNotPrintItself()
        {
            new SspEncryptionKey(123, 456).ToString().Should().NotContain("123").And.NotContain("456");
        }
    }
}
