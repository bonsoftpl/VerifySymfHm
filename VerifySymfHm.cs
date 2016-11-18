/*
VerifySymfHm - weryfikator baz danych Sage Symfonia Handel
Copyright (C) 2016, jaroslaw.czekalski@bonsoft.pl
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
      Console.WriteLine("VerifySymfHm, licensed with GNU GPL 3.0, (c) 2016 bonsoft.pl");
      Console.WriteLine("VerifySymfHm, start " + DateTime.Now);
      m_seti = ConfigurationManager.AppSettings;
      m_sConnStr = m_seti["ConnectionString"].ToString();
      m_idDwMin = Int32.Parse(m_seti["MinIdDw"]);
      m_conn = new OdbcConnection(m_sConnStr);
      m_conn.Open();
    }

    protected void SprawdzWartNowychDostaw()
    {
      var cmd = m_conn.CreateCommand();
      cmd.CommandTimeout = 60;
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
      cmd.CommandTimeout = 60;
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
      c.Close();
    }
  }
}
