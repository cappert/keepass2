﻿/*
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

using KeePass.Native;

using KeePassLib.Utility;

namespace KeePass.Util
{
	public static partial class SendInputEx
	{
		private static void EnsureSameKeyboardLayout(SiStateEx si)
		{
			IntPtr hklSelf = NativeMethods.GetKeyboardLayout(0);
			IntPtr hklTarget = NativeMethods.GetKeyboardLayout(si.TargetThreadID);

			si.CurrentKeyboardLayout = hklSelf;

			if(!Program.Config.Integration.AutoTypeAdjustKeyboardLayout) return;

			if(hklSelf != hklTarget)
			{
				si.OriginalKeyboardLayout = NativeMethods.ActivateKeyboardLayout(
					hklTarget, 0);
				si.CurrentKeyboardLayout = hklTarget;

				Debug.Assert(si.OriginalKeyboardLayout == hklSelf);
			}

			// ushort uLangID = (ushort)(si.CurrentKeyboardLayout.ToInt64() & 0xFFFF);
			// si.EnableCaretWorkaround = (uLangID == LangIDGerman);
		}

		private static bool SendVKeyNative(int vKey, bool bDown)
		{
			bool bRes = false;

			if(bDown || IsKeyActive(vKey))
			{
				if(IntPtr.Size == 4) bRes = SendVKeyNative32(vKey, null, bDown);
				else if(IntPtr.Size == 8) bRes = SendVKeyNative64(vKey, null, bDown);
				else { Debug.Assert(false); }
			}

			if(bDown && (vKey != NativeMethods.VK_CAPITAL))
			{
				Debug.Assert(IsKeyActive(vKey));
			}

			return bRes;
		}

		private static bool SendCharNative(char ch)
		{
			bool bRes = SendCharNative(ch, true);
			if(!SendCharNative(ch, false)) bRes = false; // Not &= (short-circuit)
			return bRes;
		}

		private static bool SendCharNative(char ch, bool bDown)
		{
			if(IntPtr.Size == 4) return SendVKeyNative32(0, ch, bDown);
			else if(IntPtr.Size == 8) return SendVKeyNative64(0, ch, bDown);
			else { Debug.Assert(false); }

			return false;
		}

		private static bool SendVKeyNative32(int vKey, char? optUnicodeChar, bool bDown)
		{
			NativeMethods.INPUT32[] pInput = new NativeMethods.INPUT32[1];

			pInput[0].Type = NativeMethods.INPUT_KEYBOARD;

			if(optUnicodeChar.HasValue && WinUtil.IsAtLeastWindows2000)
			{
				pInput[0].KeyboardInput.VirtualKeyCode = 0;
				pInput[0].KeyboardInput.ScanCode = (ushort)optUnicodeChar.Value;
				pInput[0].KeyboardInput.Flags = ((bDown ? 0 :
					NativeMethods.KEYEVENTF_KEYUP) | NativeMethods.KEYEVENTF_UNICODE);
			}
			else // Standard VKey
			{
				if(optUnicodeChar.HasValue)
					vKey = ((int)NativeMethods.VkKeyScan(optUnicodeChar.Value) & 0xFF);

				pInput[0].KeyboardInput.VirtualKeyCode = (ushort)vKey;
				pInput[0].KeyboardInput.ScanCode =
					(ushort)(NativeMethods.MapVirtualKey((uint)vKey, 0) & 0xFFU);
				pInput[0].KeyboardInput.Flags = GetKeyEventFlags(vKey, bDown);
			}

			pInput[0].KeyboardInput.Time = 0;
			pInput[0].KeyboardInput.ExtraInfo = NativeMethods.GetMessageExtraInfo();

			Debug.Assert(Marshal.SizeOf(typeof(NativeMethods.INPUT32)) == 28);
			if(NativeMethods.SendInput32(1, pInput,
				Marshal.SizeOf(typeof(NativeMethods.INPUT32))) != 1)
				return false;

			return true;
		}

		private static bool SendVKeyNative64(int vKey, char? optUnicodeChar, bool bDown)
		{
			NativeMethods.SpecializedKeyboardINPUT64[] pInput = new
				NativeMethods.SpecializedKeyboardINPUT64[1];

			pInput[0].Type = NativeMethods.INPUT_KEYBOARD;

			if(optUnicodeChar.HasValue && WinUtil.IsAtLeastWindows2000)
			{
				pInput[0].VirtualKeyCode = 0;
				pInput[0].ScanCode = (ushort)optUnicodeChar.Value;
				pInput[0].Flags = ((bDown ? 0 : NativeMethods.KEYEVENTF_KEYUP) |
					NativeMethods.KEYEVENTF_UNICODE);
			}
			else // Standard VKey
			{
				if(optUnicodeChar.HasValue)
					vKey = ((int)NativeMethods.VkKeyScan(optUnicodeChar.Value) & 0xFF);

				pInput[0].VirtualKeyCode = (ushort)vKey;
				pInput[0].ScanCode = (ushort)(NativeMethods.MapVirtualKey(
					(uint)vKey, 0) & 0xFFU);
				pInput[0].Flags = GetKeyEventFlags(vKey, bDown);
			}

			pInput[0].Time = 0;
			pInput[0].ExtraInfo = NativeMethods.GetMessageExtraInfo();

			Debug.Assert(Marshal.SizeOf(typeof(NativeMethods.SpecializedKeyboardINPUT64)) == 40);
			if(NativeMethods.SendInput64Special(1, pInput,
				Marshal.SizeOf(typeof(NativeMethods.SpecializedKeyboardINPUT64))) != 1)
				return false;

			return true;
		}

		private static uint GetKeyEventFlags(int vKey, bool bDown)
		{
			uint u = 0;
			if(!bDown) u |= NativeMethods.KEYEVENTF_KEYUP;
			if(IsExtendedKeyEx(vKey)) u |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
			return u;
		}

		private static bool IsExtendedKeyEx(int vKey)
		{
			// http://msdn.microsoft.com/en-us/library/windows/desktop/dd375731.aspx
			// http://www.win.tue.nl/~aeb/linux/kbd/scancodes-1.html
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_LSHIFT, 0) == 0x2AU);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_RSHIFT, 0) == 0x36U);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_SHIFT, 0) == 0x2AU);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_LCONTROL, 0) == 0x1DU);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_RCONTROL, 0) == 0x1DU);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_CONTROL, 0) == 0x1DU);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_LMENU, 0) == 0x38U);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_RMENU, 0) == 0x38U);
			Debug.Assert(NativeMethods.MapVirtualKey((uint)
				NativeMethods.VK_MENU, 0) == 0x38U);
			Debug.Assert(NativeMethods.MapVirtualKey(0x5BU, 0) == 0x5BU);
			Debug.Assert(NativeMethods.MapVirtualKey(0x5CU, 0) == 0x5CU);
			Debug.Assert(NativeMethods.MapVirtualKey(0x5DU, 0) == 0x5DU);
			Debug.Assert(NativeMethods.MapVirtualKey(0x6AU, 0) == 0x37U);
			Debug.Assert(NativeMethods.MapVirtualKey(0x6BU, 0) == 0x4EU);
			Debug.Assert(NativeMethods.MapVirtualKey(0x6DU, 0) == 0x4AU);
			Debug.Assert(NativeMethods.MapVirtualKey(0x6EU, 0) == 0x53U);
			Debug.Assert(NativeMethods.MapVirtualKey(0x6FU, 0) == 0x35U);

			if((vKey >= 0x21) && (vKey <= 0x2E)) return true;
			if((vKey >= 0x5B) && (vKey <= 0x5D)) return true;
			if(vKey == 0x6F) return true; // VK_DIVIDE

			// RShift is separate; no E0
			if(vKey == NativeMethods.VK_RCONTROL) return true;
			if(vKey == NativeMethods.VK_RMENU) return true;

			return false;
		}

		private static List<int> GetActiveKeyModifiers()
		{
			List<int> lSet = new List<int>();

			AddKeyModifierIfSet(lSet, NativeMethods.VK_LSHIFT);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_RSHIFT);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_SHIFT);

			AddKeyModifierIfSet(lSet, NativeMethods.VK_LCONTROL);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_RCONTROL);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_CONTROL);

			AddKeyModifierIfSet(lSet, NativeMethods.VK_LMENU);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_RMENU);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_MENU);

			AddKeyModifierIfSet(lSet, NativeMethods.VK_LWIN);
			AddKeyModifierIfSet(lSet, NativeMethods.VK_RWIN);

			AddKeyModifierIfSet(lSet, NativeMethods.VK_CAPITAL);

			return lSet;
		}

		private static void AddKeyModifierIfSet(List<int> lList, int vKey)
		{
			if(IsKeyActive(vKey)) lList.Add(vKey);
		}

		private static bool IsKeyActive(int vKey)
		{
			if(vKey == NativeMethods.VK_CAPITAL)
			{
				ushort usCap = NativeMethods.GetKeyState(vKey);
				return ((usCap & 1) != 0);
			}

			ushort usState = NativeMethods.GetAsyncKeyState(vKey);
			return ((usState & 0x8000) != 0);

			// For GetKeyState:
			// if(vKey == NativeMethods.VK_CAPITAL)
			//	return ((usState & 1) != 0);
			// else
			//	return ((usState & 0x8000) != 0);
		}

		private static void ActivateKeyModifiers(List<int> vKeys, bool bDown)
		{
			Debug.Assert(vKeys != null);
			if(vKeys == null) throw new ArgumentNullException("vKeys");

			foreach(int vKey in vKeys)
			{
				if(vKey == NativeMethods.VK_CAPITAL) // Toggle
				{
					SendVKeyNative(vKey, true);
					SendVKeyNative(vKey, false);
				}
				else SendVKeyNative(vKey, bDown);
			}
		}

		private static void SpecialReleaseModifiers(List<int> vKeys)
		{
			// Get out of a menu bar that was focused when only
			// using Alt as hot key modifier
			if(Program.Config.Integration.AutoTypeReleaseAltWithKeyPress &&
				(vKeys.Count == 2) && vKeys.Contains(NativeMethods.VK_MENU))
			{
				if(vKeys.Contains(NativeMethods.VK_LMENU))
				{
					SendVKeyNative(NativeMethods.VK_LMENU, true);
					SendVKeyNative(NativeMethods.VK_LMENU, false);
				}
				else if(vKeys.Contains(NativeMethods.VK_RMENU))
				{
					SendVKeyNative(NativeMethods.VK_RMENU, true);
					SendVKeyNative(NativeMethods.VK_RMENU, false);
				}
			}
		}

		/* private static void OSSendKeysWindows(string strSequence)
		{
			// Workaround for ^/& .NET SendKeys bug:
			// https://connect.microsoft.com/VisualStudio/feedback/details/93922/sendkeys-send-sends-wrong-character
			string[] vSend = strSequence.Split(new string[] { @"{^}" },
				StringSplitOptions.None);
			bool bHat = false;
			foreach(string strSend in vSend)
			{
				if(bHat)
				{
					// ushort usCaret = NativeMethods.VkKeyScan('^');
					// if(usCaret != 0xFFFF)
					// {
					//	int vkCaret = (int)(usCaret & 0xFF);
					//	SendVKeyNative(vkCaret, true);
					//	SendVKeyNative(vkCaret, false);
					//	Thread.Sleep(20);
					//	OSSendKeysWindows(@"{+}{BACKSPACE}");
					// }
					// else { Debug.Assert(false); }

					SendCharNative('^');
				}

				if(!string.IsNullOrEmpty(strSend)) SendKeys.SendWait(strSend);

				bHat = true;
			}
		} */

		private static Dictionary<string, char> m_dictNativeChars = null;
		private static string[] m_vNativeCharKeys = null;
		private static void OSSendKeysWindows(string strSequence)
		{
			// Workaround for ^/& .NET SendKeys bug:
			// https://connect.microsoft.com/VisualStudio/feedback/details/93922/sendkeys-send-sends-wrong-character

			if(m_dictNativeChars == null)
			{
				m_dictNativeChars = new Dictionary<string, char>();
				m_dictNativeChars[@"{^}"] = '^';
				m_dictNativeChars[@"{%}"] = '%';
				m_dictNativeChars[@"´"] = '´';
				m_dictNativeChars[@"`"] = '`';
				m_dictNativeChars[@"@"] = '@';
				m_dictNativeChars[@"°"] = '°';
				m_dictNativeChars[@"£"] = '£';
				m_dictNativeChars[@"|"] = '|';

				List<string> lKeys = new List<string>(m_dictNativeChars.Keys);
				m_vNativeCharKeys = lKeys.ToArray();
			}

			List<string> vSend = StrUtil.SplitWithSep(strSequence,
				m_vNativeCharKeys, true);

			foreach(string strSend in vSend)
			{
				if(string.IsNullOrEmpty(strSend)) continue;

				char chNative;
				if(m_dictNativeChars.TryGetValue(strSend, out chNative))
					SendCharNative(chNative);
				else SendKeys.SendWait(strSend);
			}
		}
	}
}
