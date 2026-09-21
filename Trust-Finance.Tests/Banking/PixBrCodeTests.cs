using TrustFinance.Domain.Banking;

namespace TrustFinance.Tests.Banking;

public class PixBrCodeTests
{
    // A static code with a phone key, an amount, a description and a txid; CRC computed apart from the code under test.
    private const string Valid = "00020126570014br.gov.bcb.pix0114+55119999999990217Padaria do Bairro5204000053039865406123.455802BR5913FULANO DE TAL6009SAO PAULO62090505TX1236304EAF0";

    [Fact]
    public void A_Valid_Code_Should_Decode_Every_Field()
    {
        PixBrCode.TryParse(Valid, out var info, out var error).Should().BeTrue(error);

        info.Key.Should().Be("+5511999999999");
        info.MerchantName.Should().Be("FULANO DE TAL");
        info.MerchantCity.Should().Be("SAO PAULO");
        info.Amount.Should().Be(123.45m);
        info.Description.Should().Be("Padaria do Bairro");
        info.TransactionId.Should().Be("TX123");
        info.IsDynamic.Should().BeFalse();
    }

    [Fact]
    public void A_Changed_Digit_Should_Fail_The_Checksum()
    {
        var tampered = Valid.Replace("5406123.45", "5406193.45");

        PixBrCode.TryParse(tampered, out _, out var error).Should().BeFalse();
        error.Should().Contain("verificação");
    }

    [Fact]
    public void An_Emv_Code_That_Is_Not_Pix_Should_Be_Refused()
    {
        var account = "0014br.gov.bcb.xyz0114+5511999999999";
        var body = $"000201{"26"}{account.Length:00}{account}5303986{"5802BR"}{"5913FULANO DE TAL"}{"6009SAO PAULO"}6304";
        var code = body + PixBrCode.Crc16(body).ToString("X4");

        PixBrCode.TryParse(code, out _, out var error).Should().BeFalse();
        error.Should().Contain("não é Pix");
    }

    [Fact]
    public void Something_That_Is_Not_A_Code_Should_Say_So()
    {
        PixBrCode.TryParse("olá", out _, out var error).Should().BeFalse();
        error.Should().Contain("000201");
    }

    [Fact]
    public void Crc16_Should_Match_The_Reference_Vector()
    {
        // CRC-16/CCITT-FALSE of "123456789" is 0x29B1 in every reference table.
        PixBrCode.Crc16("123456789").Should().Be(0x29B1);
    }

    [Fact]
    public void A_Code_Without_An_Amount_Should_Leave_It_Open()
    {
        var account = "0014br.gov.bcb.pix0114+5511999999999";
        var body = $"000201{"26"}{account.Length:00}{account}5204000053039865802BR5913FULANO DE TAL6009SAO PAULO62070503***6304";
        var code = body + PixBrCode.Crc16(body).ToString("X4");

        PixBrCode.TryParse(code, out var info, out var error).Should().BeTrue(error);
        info.Amount.Should().BeNull();
        info.TransactionId.Should().BeNull("*** means no id was set");
    }
}
