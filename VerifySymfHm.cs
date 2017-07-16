/*
VerifySymfHm - weryfikator baz danych Sage Symfonia Handel
Copyright (C) 2016-17, jaroslaw.czekalski@bonsoft.pl
https://github.com/bonsoftpl/VerifySymfHm

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerifySymfHm
{
  class VerifySymfHm
  {
    protected OdbcConnection m_conn;
    protected string m_sConnStr;
    protected int m_idDwMin;
    protected NameValueCollection m_seti;
    protected StringBuilder m_sbOut;
    protected bool m_bDebug;

    public void WriteLine(string s)
    {
      m_sbOut.AppendLine(s);
      Console.WriteLine(s);
    }

    public VerifySymfHm()
    {
      Console.WriteLine("VerifySymfHm " +
        Assembly.GetExecutingAssembly().GetName().Version.ToString() +
        ", licensed with GNU GPL 3.0, (c) 2016-17 bonsoft.pl");
      m_sbOut = new StringBuilder();
      Console.WriteLine("VerifySymfHm, start " + DateTime.Now);
      m_seti = ConfigurationManager.AppSettings;
      m_bDebug = GetSetting(m_seti, "Debug", false);
      m_sConnStr = m_seti["ConnectionString"].ToString();
      m_idDwMin = Int32.Parse(m_seti["MinIdDw"]);
      m_conn = new OdbcConnection(m_sConnStr);
      m_conn.Open();
    }

    protected int DajMinIdMzDlaDaty(string sDataOd)
    {
      /* Nie potrafię tego zrobić w jednym query z subquery, bo w poniższym
       * query pervasive nie przyjmuje wewnętrznego order by
       * Dlatego rozbijam na 2 zapytania.
      select top 1 imz1.id from imz imz1 where imz1.data = (
        select top 1 imz2.data from imz imz2
        where imz2.data < '2017-01-24'
        order by imz2.data
      )
      order by imz1.id
      */

      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select top 1 data from imz " +
        "  where data < '" + sDataOd + "' " +
        "  order by data desc ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      string sDataPrzed = "";
      if (rs.Read())
        sDataPrzed = rs["data"].ToString();
      cmd.Dispose();
      if (sDataPrzed == "")
        return 0;

      cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select top 1 id from imz " +
        "with (index(\"idx_data\")) " +
        "where data = '" + sDataPrzed + "' " +
        "order by id ";
      DumpSqlMaybe(cmd);
      rs = cmd.ExecuteReader();
      int id = 0;
      if (rs.Read())
        id = (int)rs["id"];
      cmd.Dispose();
      return id;
    }

    private void DumpSqlMaybe(OdbcCommand cmd)
    {
      if (!m_bDebug)
        return;
      new StackTrace(1).GetFrames().Take(1).ToList().ForEach(it => {
        Console.WriteLine(it.ToString());
      });
      Console.WriteLine("executing " + cmd.CommandText);
      Console.WriteLine();
    }

    protected int DajMinIdPwDlaDaty(string sDataOd)
    {
      int idMz = DajMinIdMzDlaDaty(sDataOd);
      int idPw = 0;
      
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select top 1 id from pw " +
        "  where typi = 37 and idmg = " + idMz;
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      if (rs.Read())
        idPw = rs.GetInt32(0);
      cmd.Dispose();

      if (idPw == 0)
        m_sbOut.AppendLine("Nie udało się pobrać idPw min.");

      return idPw;
    }

    protected void SprawdzWartNowychDostaw()
    {
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select " +
        "dw.kod as kodDw, dw.data as data, tw.kod as kodTow " +
        ", wartoscDoSp, wartoscSt, ilosc, iloscpz, dw.stan " +
        ", dw.id as iddw, xt.kod as mag " +
        "from dw " +
        "left join tw on dw.idtw = tw.id " +
        "left join xt on dw.magazyn = xt.id " +
        "where dw.id > " + m_idDwMin +
        "and round(dw.wartoscDoSp, 2) <> round(dw.wartoscSt, 2) " +
        "and round(dw.ilosc, 3) = round(dw.iloscpz, 3) " +
        "and round(dw.ilosc, 3) = round(dw.stan, 3) ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read()) {
        WriteLine(String.Format("Dostawa {0} (id {6}) z dnia {1}, " +
          "towar {2}, " +
          "mag {5}, ma wart. pocz. {3}, a obecną {4}.", rs["kodDw"], rs["data"],
          rs["kodTow"], rs["wartoscDoSp"], rs["wartoscSt"], rs["mag"],
          rs["iddw"]));
      }
      cmd.Dispose();
    }

    protected void WypiszKorektyCenDoDw(int iddw)
    {
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select mg.kod as kodPzk " +
        ", pw.wartosc as wartosc, mz.cena as cena " +
        "from pw " +
        "left join mz on pw.idmg = mz.id " +
        "left join mg on mz.super = mg.id " +
        "where pw.typi = 37 and pw.iddw = " + iddw + " " +
        "and pw.subtypi = 89 and pw.flag in (16, 20) ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      while (rs.Read()) {
        WriteLine(String.Format("Ale jest korekta ceny {0} "
          + " na kwotę {1} (cena {2}?).",
          rs["kodPzk"], rs["wartosc"], rs["cena"]));
      }
      cmd.Dispose();
    }

    protected void SprawdzCenyWydanZDostaw()
    {
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      string sDataOd = DateTime.Now
        .Subtract(new TimeSpan(Int32.Parse(m_seti["DaysBack"]), 0, 0, 0))
        .ToString("yyyy-MM-dd");
      cmd.CommandText = "select dw.kod as kodDw, mg.kod as kodWyd " +
        ", dw.data as dataDw, pw.wartosc / pw.ilosc as cenaPw " +
        ", dw.cena as cenaDw, tw.kod as kodTow, pw.id as idPw " +
        ", dw.id as idDw " +
        "from pw " +
        "left join dw on pw.iddw = dw.id " +
        "left join tw on pw.idtw = tw.id " +
        "left join mz on pw.idmg = mz.id " +
        "left join mg on mz.super = mg.id " +
        "where pw.typi = 37 and pw.data >= '" + sDataOd + "' " +
        "and not pw.ilosc between -0.9 and 0.9 " +
        "and not pw.wartosc / pw.ilosc - dw.cena between - 0.2 and 0.2 ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read()) {
        WriteLine(String.Format("Wydanie {0} ({2}, id {6}) " +
          "z dostawy {1} (iddw={7}) " +
          "ma bledna cene (pw={3}, dw={4}), towar {5}.",
          rs["kodWyd"], rs["kodDw"], rs["dataDw"], rs["cenaPw"], rs["cenaDw"],
          rs["kodTow"], rs["idPw"], rs["idDw"]));
        WypiszKorektyCenDoDw(Convert.ToInt32(rs["idDw"]));
      }
      cmd.Dispose();
    }

    protected void SprawdzCzyMzMajaPw()
    {
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      string sDataOd = DateTime.Now
        .Subtract(new TimeSpan(Int32.Parse(m_seti["DaysBack"]), 0, 0, 0))
        .ToString("yyyy-MM-dd");
      cmd.CommandText = "select mg.kod as kodMg, mg.data as data, " +
        "mz.kod as kodTow, mz.id as idMz " +
        "from mg " +
        "left join mz on mz.super = mg.id " +
        "left join pw on pw.typi = 37 and pw.idmg = mz.id " +
        "where mg.data >= '" + sDataOd + "' " +
        "and mz.id is not null and pw.id is null " +
        // anulowane nas nie interesują
        "and mg.subtypi <> 0 " +
        // pozycje kompletow nie maja pw
        "and (mz.flag & 4096) = 0 " +
        // niektóre pozycje są naprawdę zerowe i ich się nie czepiamy
        "and not ((mz.ilosc between - 0.0001 and 0.0001) " +
        "         and (mz.cena between - 0.001 and 0.001)) ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read())
      {
        WriteLine(String.Format("W dok. mag. {0} ({1}) " +
          "brakuje pw do pozycji {2} (mz:{3}) ",
          rs["kodMg"], rs["data"], rs["kodTow"], rs["idMz"]));
      }
      cmd.Dispose();
    }

    protected void SprawdzWartMzPw()
    {
      string sDataOd = DateTime.Now
        .Subtract(new TimeSpan(Int32.Parse(m_seti["DaysBack"]), 0, 0, 0))
        .ToString("yyyy-MM-dd");
      int idMz = DajMinIdMzDlaDaty(sDataOd);
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText = "select mz1.id as idMz, mg.kod as kodMg, " +
        "mz1.data as data, mz1.wartNetto as wartMz, " +
        "tw.kod as kodTow, " +
        "round(sum(pw3.wartosc) - mz1.wartNetto, 2) as roznica " +
        "from mz mz1 " +
        "left join tw on tw.id = mz1.idtw " +
        "left join mg on mg.id = mz1.super " +
        "left join pw pw3 on pw3.typi = 37 and pw3.idmg = mz1.id " +
        "where mz1.typi <> 1 " +
        "and mz1.id > " + idMz +
        "and " +
        "( " +
        "  select round(sum(pw1.wartosc), 2) " +
        "  from pw pw1 " +
        "  where pw1.typi = 37 and pw1.idmg = mz1.id " +
        ") <> round(mz1.wartNetto, 2) " +
        "group by mz1.id, mg.kod, mz1.data, mz1.wartNetto, tw.kod ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      while (rs.Read()) {
        WriteLine(String.Format("SprawdzWartMzPw: niezgodnosc w " +
          "dok. mag. {0} ({1}), towar {2} (mz:{3}), na kwote {4}",
          rs["kodMg"], rs["data"], rs["kodTow"], rs["idMz"], rs["roznica"]));
      }
      cmd.Dispose();
    }

    protected void SprawdzWiszaceRezerwacje()
    {
      string sDataOd = DateTime.Now
        .Subtract(new TimeSpan(Int32.Parse(m_seti["DaysBack"]), 0, 0, 0))
        .ToString("yyyy-MM-dd");
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = Int32.Parse(m_seti["Timeout"].ToString());
      cmd.CommandText =
        "select tw.kod as kodTow, dw.kod as kodDw from pw " +
        "left join tw on pw.idtw = tw.id " +
        "left join dw on pw.iddw = dw.id " +
        "where pw.typi = 26 " +
        "and not exists ( " +
        "  select id from zz where zz.id = pw.idmg " +
        ") ";
      DumpSqlMaybe(cmd);
      var rs = cmd.ExecuteReader();
      while (rs.Read()) {
        WriteLine(String.Format("SprawdzWiszaceRezerwacje: " +
          "towar {0}, dostawa {1}",
          rs["kodTow"], rs["kodDw"]));
      }
      cmd.Dispose();
    }

    public void Close()
    {
      m_conn.Close();
      Console.WriteLine("VerifySymfHm, end " + DateTime.Now);
    }

    public void SendReport()
    {
      string sSubj = "Prawdopodobne błędy w bazie Symfonia Handel";
      string sBody = "Poniższe błędy czasem wynikają z niedoskonałości "
        + "procedur diagnostycznych. Na przykład po korekcie przyjęcia "
        + "program wykazuje niezgodności na cenach wydań."
        + Environment.NewLine + Environment.NewLine;
      sBody += m_sbOut.ToString();
      if (m_seti["OkreslenieBazy"] != null)
        sSubj += " " + m_seti["OkreslenieBazy"].ToString();
      bool bSend = m_sbOut.Length > 0
                   || GetSetting(m_seti, "ForceEmail", false);
      if (bSend && m_seti["SmtpHost"] != null) {
        Console.Write("Sending email, beacuse SmtpHost is set...");
        var mail = new MailMessage(
          m_seti["MailFrom"].ToString(),
          m_seti["MailTo"].ToString(),
          sSubj,
          sBody
        );
        var smtp = new SmtpClient(m_seti["SmtpHost"]);
        smtp.Timeout = 10000;
        if (m_seti["SmtpPort"] != null)
          smtp.Port = Int32.Parse(m_seti["SmtpPort"].ToString());
        if (m_seti["SmtpPass"] != null)
          smtp.Credentials = new NetworkCredential(
            m_seti["SmtpUser"].ToString(), m_seti["SmtpPass"].ToString()
          );
        smtp.EnableSsl = GetSetting(m_seti, "SmtpSsl", true);

        if (GetSetting(m_seti, "SmtpSslIgnoreErrors", false)) {
          // Switch of ssl invalid certificate error, like in
          // https://stackoverflow.com/a/1386568/772981
          ServicePointManager.ServerCertificateValidationCallback =
            delegate (object s, X509Certificate certificate,
             X509Chain chain, SslPolicyErrors sslPolicyErrors) {
               return true; };
        }

        m_seti.AllKeys.Where(k => k.StartsWith("MailCC"))
        .ToList().ForEach(k => {
          mail.CC.Add(m_seti[k]);
        });

        smtp.Send(mail);

        Console.WriteLine("done.");
      } else {
        if (m_sbOut.Length > 0)
          Console.WriteLine("SmtpHost not set, so not sending any emails.");
      }
    }

    private T GetSetting<T>(NameValueCollection seti,
      string sKey, T defaultValue)
    {
      T v = defaultValue;
      if (seti[sKey] != null)
        v = (T)Convert.ChangeType(seti[sKey].ToString(),
          Convert.GetTypeCode(v));
      return v;
    }

    static void Main(string[] args)
    {
      var c = new VerifySymfHm();
      c.SprawdzWartNowychDostaw();
      c.SprawdzCenyWydanZDostaw();
      c.SprawdzCzyMzMajaPw();
      c.SprawdzWartMzPw();
      c.SprawdzWiszaceRezerwacje();
      c.Close();
      c.SendReport();
    }
  }
}
