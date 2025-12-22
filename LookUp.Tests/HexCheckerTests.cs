using LookUp.Helpers;

namespace LookUp.Tests
{
    public class HexCheckerTests
    {
        [Theory]
        [InlineData("78726c3a746f3a415354455228425343293a307866386134613035343762314565344661613436413238353534613242424539463562353164343745")]
        [InlineData("5270363a746f3a5a454328425343293a307862343535323164364639326564373537413536656442373330333331303133383463396566363442")]
        [InlineData("3d3a4554482e555344543a3078614644314337613966633530373435323145623762444232413730644262394533326635336136343a302f312f313a656a3a3735")]
        [InlineData("6c49343a746f3a4554483a307831643345426543363644623239463230463542424539343835314237363532346437343634664437")]
        [InlineData("776a793a746f3a415354455228425343293a307839374635364445383862306661323538326134374341384562316338643032433034356433664339")]
        [InlineData("726e543a746f3a425443423a307835416546354531663332336534433137344542353430363435374235353645376336343630626433")]
        public void CanFilterOut(string hex)
        {
            bool isValid = HexChecker.FilterOutMessages(hex, out string? message);

            Assert.False(isValid);      // False, because we don't want contracts inside the DB.
            Assert.NotNull(message);    // NotNull, to see we successfully decoded it, we just don't want it in the DB:

            bool doContains = HexChecker.BannedMessageParts.Any(message.Contains);    // Message contains the smart contract part.
            Assert.True(doContains);
        }
    }
}