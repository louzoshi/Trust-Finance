using TrustFinance.Domain.Banking;

namespace TrustFinance.Tests.Banking;

public class OfxTests
{
    /// <summary>The SGML shape most Brazilian banks export: no closing tags on leaf elements.</summary>
    private const string Sgml = """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102
        SECURITY:NONE
        ENCODING:USASCII
        CHARSET:1252
        COMPRESSION:NONE
        OLDFILEUID:NONE
        NEWFILEUID:NONE

        <OFX>
        <SIGNONMSGSRSV1><SONRS><STATUS><CODE>0<SEVERITY>INFO</STATUS><DTSERVER>20260915120000[-3:BRT]<LANGUAGE>POR</SONRS></SIGNONMSGSRSV1>
        <BANKMSGSRSV1><STMTTRNRS><TRNUID>1<STATUS><CODE>0<SEVERITY>INFO</STATUS>
        <STMTRS><CURDEF>BRL
        <BANKACCTFROM><BANKID>0341<ACCTID>12345-6<ACCTTYPE>CHECKING</BANKACCTFROM>
        <BANKTRANLIST><DTSTART>20260901<DTEND>20260915
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260903000000[-3:BRT]<TRNAMT>-45.90<FITID>2026090300123<MEMO>PIX ENVIADO 03/09 MERCADO SILVA</STMTTRN>
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260905<TRNAMT>5000.00<FITID>2026090500456<NAME>SALARIO<MEMO>CRED SALARIO EMPRESA LTDA</STMTTRN>
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260910<TRNAMT>-1800,00<FITID>2026091000789<MEMO>TED ENVIADA ALUGUEL</STMTTRN>
        </BANKTRANLIST>
        <LEDGERBAL><BALAMT>3154.10<DTASOF>20260915</LEDGERBAL>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1>
        </OFX>
        """;

    [Fact]
    public void Sgml_Without_Closing_Tags_Should_Parse()
    {
        var statement = Ofx.Parse(Sgml);

        statement.BankId.Should().Be("0341");
        statement.AccountId.Should().Be("12345-6");
        statement.IsCreditCard.Should().BeFalse();
        statement.From.Should().Be(new DateOnly(2026, 9, 1));
        statement.To.Should().Be(new DateOnly(2026, 9, 15));
        statement.LedgerBalance.Should().Be(3154.10m);
        statement.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public void Each_Line_Should_Carry_The_Banks_Id_Date_Amount_And_Text()
    {
        var lines = Ofx.Parse(Sgml).Transactions;

        lines[0].FitId.Should().Be("2026090300123");
        lines[0].Date.Should().Be(new DateOnly(2026, 9, 3), "the time and zone suffix are dropped");
        lines[0].Amount.Should().Be(-45.90m);
        lines[0].Memo.Should().Be("PIX ENVIADO 03/09 MERCADO SILVA");
        lines[0].IsCredit.Should().BeFalse();

        lines[1].Memo.Should().Be("CRED SALARIO EMPRESA LTDA", "MEMO already says SALARIO, so NAME adds nothing");
        lines[1].IsCredit.Should().BeTrue();

        lines[2].Amount.Should().Be(-1800m, "a comma decimal is accepted");
    }

    [Fact]
    public void Name_Should_Be_Prepended_When_The_Memo_Does_Not_Already_Say_It()
    {
        var one = Sgml.Replace("<NAME>SALARIO<MEMO>CRED SALARIO EMPRESA LTDA", "<NAME>POSTO SHELL<MEMO>COMPRA CARTAO 12/09");

        Ofx.Parse(one).Transactions[1].Memo.Should().Be("POSTO SHELL COMPRA CARTAO 12/09");
    }

    [Fact]
    public void The_Xml_Flavour_Should_Parse_Too()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <?OFX OFXHEADER="200" VERSION="220" SECURITY="NONE" OLDFILEUID="NONE" NEWFILEUID="NONE"?>
            <OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS><CURDEF>BRL</CURDEF>
            <CCACCTFROM><ACCTID>4111********1111</ACCTID></CCACCTFROM>
            <BANKTRANLIST><DTSTART>20260801</DTSTART><DTEND>20260831</DTEND>
            <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20260812</DTPOSTED><TRNAMT>-210.40</TRNAMT><FITID>cc-1</FITID><MEMO>POSTO SHELL</MEMO></STMTTRN>
            </BANKTRANLIST></CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>
            """;

        var statement = Ofx.Parse(xml);

        statement.IsCreditCard.Should().BeTrue();
        statement.AccountId.Should().Be("4111********1111");
        statement.Transactions.Should().ContainSingle().Which.Memo.Should().Be("POSTO SHELL");
    }

    [Fact]
    public void Something_Else_Should_Be_Refused()
    {
        var act = () => Ofx.Parse("<html><body>not a statement</body></html>");

        act.Should().Throw<FormatException>().WithMessage("*OFX*");
    }
}
