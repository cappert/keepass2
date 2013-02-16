/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using KeePass.App;
using KeePass.Resources;

using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Utility;

namespace KeePass.Util
{
	public static class PwGeneratorUtil
	{
		private static string m_strBuiltInSuffix = null;
		internal static string BuiltInSuffix
		{
			get
			{
				if(m_strBuiltInSuffix == null)
					m_strBuiltInSuffix = " (" + KPRes.BuiltIn + ")";
				return m_strBuiltInSuffix;
			}
		}

		private static List<PwProfile> m_lBuiltIn = null;
		public static List<PwProfile> BuiltInProfiles
		{
			get
			{
				if(m_lBuiltIn == null) AllocStandardProfiles();
				return m_lBuiltIn;
			}
		}

		private static void AllocStandardProfiles()
		{
			m_lBuiltIn = new List<PwProfile>();

			AddStdPattern(KPRes.RandomMacAddress, @"HH\-HH\-HH\-HH\-HH\-HH");

			string strHex = KPRes.HexKey;
			AddStdPattern(strHex.Replace(@"{PARAM}", "40"), @"h{10}");
			AddStdPattern(strHex.Replace(@"{PARAM}", "128"), @"h{32}");
			AddStdPattern(strHex.Replace(@"{PARAM}", "256"), @"h{64}");
		}

		private static void AddStdPattern(string strName, string strPattern)
		{
			PwProfile p = new PwProfile();

			p.Name = strName + PwGeneratorUtil.BuiltInSuffix;
			p.CollectUserEntropy = false;
			p.GeneratorType = PasswordGeneratorType.Pattern;
			p.Pattern = strPattern;

			m_lBuiltIn.Add(p);
		}

		/// <summary>
		/// Get a list of all password generator profiles (built-in
		/// and user-defined ones).
		/// </summary>
		public static List<PwProfile> GetAllProfiles(bool bSort)
		{
			List<PwProfile> lUser = Program.Config.PasswordGenerator.UserProfiles;

			// Sort it in the configuration file
			if(bSort) lUser.Sort(PwGeneratorUtil.CompareProfilesByName);

			// Remove old built-in profiles by KeePass <= 2.17
			for(int i = lUser.Count - 1; i >= 0; --i)
			{
				if(IsBuiltInProfile(lUser[i].Name)) lUser.RemoveAt(i);
			}

			List<PwProfile> l = new List<PwProfile>();
			l.AddRange(PwGeneratorUtil.BuiltInProfiles);
			l.AddRange(lUser);
			if(bSort) l.Sort(PwGeneratorUtil.CompareProfilesByName);
			return l;
		}

		public static bool IsBuiltInProfile(string strName)
		{
			if(strName == null) { Debug.Assert(false); return false; }

			string strWithSuffix = strName + PwGeneratorUtil.BuiltInSuffix;
			foreach(PwProfile p in PwGeneratorUtil.BuiltInProfiles)
			{
				if(p.Name.Equals(strName, StrUtil.CaseIgnoreCmp) ||
					p.Name.Equals(strWithSuffix, StrUtil.CaseIgnoreCmp))
					return true;
			}

			return false;
		}

		public static int CompareProfilesByName(PwProfile a, PwProfile b)
		{
			if(a == b) return 0;
			if(a == null) { Debug.Assert(false); return -1; }
			if(b == null) { Debug.Assert(false); return 1; }

			return StrUtil.CompareNaturally(a.Name, b.Name);
		}
	}
}
