// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Foundation
{
    public static class MacRuntimeEnvironment
    {
        static readonly NSOperatingSystemVersion mojave = new NSOperatingSystemVersion(10, 14, 0);

        public static bool MojaveOrNewer { get; } = NSProcessInfo.ProcessInfo.IsOperatingSystemAtLeastVersion(mojave);
    }
}
