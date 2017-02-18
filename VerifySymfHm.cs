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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VerifySymfHm
{
  class VerifySymfHm
  {
    protected OdbcConnection m_conn;
    protected string m_sConnStr;
    protected int m_idDwMin;
    protected NameValueCollection m_seti;

    public VerifySymfHm()
    {
      Console.WriteLine("VerifySymfHm " +
        Assembly.GetExecutingAssembly().GetName().Version.ToString() +
        ", licensed with GNU GPL 3.0, (c) 2016-17 bonsoft.pl");
      Console.WriteLine("VerifySymfHm, start " + DateTime.Now);
      m_seti = ConfigurationManager.AppSettings;
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
      rs = cmd.ExecuteReader();
      int id = 0;
      if (rs.Read())
        id = (int)rs["id"];
      cmd.Dispose();
      return id;
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
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read()) {
        Console.WriteLine(String.Format("Dostawa {0} (id {6}) z dnia {1}, " +
          "towar {2}, " +
          "mag {5}, ma wart. pocz. {3}, a obecną {4}.", rs["kodDw"], rs["data"],
          rs["kodTow"], rs["wartoscDoSp"], rs["wartoscSt"], rs["mag"],
          rs["iddw"]));
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
        "from pw " +
        "left join dw on pw.iddw = dw.id " +
        "left join tw on pw.idtw = tw.id " +
        "left join mz on pw.idmg = mz.id " +
        "left join mg on mz.super = mg.id " +
        "where pw.typi = 37 and pw.data >= '" + sDataOd + "' " +
        "and not pw.ilosc between -0.9 and 0.9 " +
        "and not pw.wartosc / pw.ilosc - dw.cena between - 0.2 and 0.2 ";
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read()) {
        Console.WriteLine(String.Format("Wydanie {0} ({2}, id {6}) z dostawy {1} " +
          "ma bledna cene (pw={3}, dw={4}), towar {5}.",
          rs["kodWyd"], rs["kodDw"], rs["dataDw"], rs["cenaPw"], rs["cenaDw"],
          rs["kodTow"], rs["idPw"]));
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
      var rs = cmd.ExecuteReader();
      //Console.WriteLine(cmd.CommandText);
      while (rs.Read())
      {
        Console.WriteLine(String.Format("W dok. mag. {0} ({1}) " +
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
      var rs = cmd.ExecuteReader();
      while (rs.Read()) {
        Console.WriteLine(String.Format("SprawdzWartMzPw: niezgodnosc w " +
          "dok. mag. {0} ({1}), towar {2} (mz:{3}), na kwote {4}",
          rs["kodMg"], rs["data"], rs["kodTow"], rs["idMz"], rs["roznica"]));
      }
      cmd.Dispose();
    }

    public void Close()
    {
      m_conn.Close();
      Console.WriteLine("VerifySymfHm, end " + DateTime.Now);
    }

    static void Main(string[] args)
    {
      var c = new VerifySymfHm();
      c.SprawdzWartNowychDostaw();
      c.SprawdzCenyWydanZDostaw();
      c.SprawdzCzyMzMajaPw();
      c.SprawdzWartMzPw();
      c.Close();
    }
  }
}
