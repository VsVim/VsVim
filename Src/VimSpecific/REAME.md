# Vim Specific 

This is a shared source project that provides Vim services which are host specific. Specifically though 
it's the subset of the VsSpecific layer which can be used during our test hosting in VimApp and unit testing.

Projects which reference this need to do the following:

- Ensure `IVimHost.HostIdentifier` is implemented as `VimSpecificUtil.HostIdentifier`.
- Ensure `<VsVimSpecificTestHost>true<VsVimSpecificTestHost>` is specified in the project file.