﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WindowsRuntime.InteropServices.Marshalling;
using static System.Runtime.InteropServices.ComWrappers;

namespace WindowsRuntime.InteropServices;

/// <summary>
/// The <c>IStringable</c> implementation for managed types.
/// </summary>
/// <see href="https://learn.microsoft.com/windows/win32/api/windows.foundation/nn-windows-foundation-istringable"/>
internal static unsafe class IStringableImpl
{
    /// <summary>
    /// The vtable for the <c>IStringable</c> implementation.
    /// </summary>
    public static nint AbiToProjectionVftablePtr { get; } = GetAbiToProjectionVftablePtr();

    /// <summary>
    /// Computes the <c>IStringable</c> implementation vtable.
    /// </summary>
    private static nint GetAbiToProjectionVftablePtr()
    {
        IStringableVftbl* vftbl = (IStringableVftbl*)WindowsRuntimeHelpers.AllocateTypeAssociatedInspectableVtable(typeof(IStringableImpl), 7);

        vftbl->ToString = &ToString;

        return (nint)vftbl;
    }

    /// <see href="https://learn.microsoft.com/windows/win32/api/windows.foundation/nf-windows-foundation-istringable-tostring"/>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static HRESULT ToString(void* thisPtr, HSTRING* value)
    {
        *value = null;

        try
        {
            object instance = ComInterfaceDispatch.GetInstance<object>((ComInterfaceDispatch*)thisPtr);

            *value = HStringMarshaller.ConvertToUnmanaged(instance.ToString());

            return WellKnownErrorCodes.S_OK;
        }
        catch (Exception e)
        {
            return RestrictedErrorInfoExceptionMarshaller.ConvertToUnmanaged(e);
        }
    }
}
