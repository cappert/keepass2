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
using System.Windows.Forms;
using System.Diagnostics;

using KeePass.Native;

namespace KeePass.UI
{
	// public delegate string QueryToolTipDelegate(TreeNode tn);

	public sealed class CustomTreeViewEx : TreeView
	{
		// private QueryToolTipDelegate m_fnQueryToolTip = null;
		// /// <summary>
		// /// This handler will be used to dynamically query tooltip
		// /// texts for tree nodes.
		// /// </summary>
		// public QueryToolTipDelegate QueryToolTip
		// {
		//	get { return m_fnQueryToolTip; }
		//	set { m_fnQueryToolTip = value; }
		// }

		public CustomTreeViewEx() : base()
		{
			// Double-buffering isn't supported by tree views
			// try { this.DoubleBuffered = true; }
			// catch(Exception) { Debug.Assert(false); }

			// try
			// {
			//	IntPtr hWnd = this.Handle;
			//	if((hWnd != IntPtr.Zero) && (this.ItemHeight == 16))
			//	{
			//		int nStyle = NativeMethods.GetWindowStyle(hWnd);
			//		nStyle |= (int)NativeMethods.TVS_NONEVENHEIGHT;
			//		NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_STYLE, nStyle);
			//		this.ItemHeight = 17;
			//	}
			// }
			// catch(Exception) { }

			// this.ItemHeight = 18;

			// try
			// {
			//	IntPtr hWnd = this.Handle;
			//	if((hWnd != IntPtr.Zero) && UIUtil.VistaStyleListsSupported)
			//	{
			//		int nStyle = NativeMethods.GetWindowStyle(hWnd);
			//		nStyle |= (int)NativeMethods.TVS_FULLROWSELECT;
			//		NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_STYLE, nStyle);
			//		nStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
			//		nStyle |= (int)NativeMethods.TVS_EX_FADEINOUTEXPANDOS;
			//		NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, nStyle);
			//	}
			// }
			// catch(Exception) { }
		}

		// protected override void WndProc(ref Message m)
		// {
		//	if(m.Msg == NativeMethods.WM_NOTIFY)
		//	{
		//		NativeMethods.NMHDR nm = (NativeMethods.NMHDR)m.GetLParam(
		//			typeof(NativeMethods.NMHDR));
		//		if((nm.code == NativeMethods.TTN_NEEDTEXTA) ||
		//			(nm.code == NativeMethods.TTN_NEEDTEXTW))
		//			DynamicAssignNodeToolTip();
		//	}
		//	base.WndProc(ref m);
		// }

		// private void DynamicAssignNodeToolTip()
		// {
		//	if(m_fnQueryToolTip == null) return;
		//	TreeViewHitTestInfo hti = HitTest(PointToClient(Cursor.Position));
		//	if(hti == null) { Debug.Assert(false); return; }
		//	TreeNode tn = hti.Node;
		//	if(tn == null) return;
		//	tn.ToolTipText = (m_fnQueryToolTip(tn) ?? string.Empty);
		// }
	}
}
