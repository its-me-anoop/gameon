#import <AppKit/AppKit.h>
#include <dlfcn.h>
#include <cassert>
#include <cstdio>
#include <initializer_list>

int main(int argc, const char **argv) {
    @autoreleasepool {
        assert(argc == 2);
        void *library = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
        if (!library) { fprintf(stderr, "%s\n", dlerror()); return 1; }
        auto initialize = reinterpret_cast<int (*)()>(dlsym(library, "Clinic_InitializeScrollMonitor"));
        auto precise = reinterpret_cast<int (*)()>(dlsym(library, "Clinic_IsPreciseScroll"));
        auto shutdown = reinterpret_cast<void (*)()>(dlsym(library, "Clinic_ShutdownScrollMonitor"));
        assert(initialize && precise && shutdown);
        [NSApplication sharedApplication];
        assert(initialize() == 1);
        assert(initialize() == 1); // An import/rebind cannot install duplicate monitors.
        assert(precise() == 0);
        for (int wanted : {1, 1, 0, 1, 0}) {
            CGEventRef cg = CGEventCreateScrollWheelEvent(nullptr,
                wanted ? kCGScrollEventUnitPixel : kCGScrollEventUnitLine, 2, 1, 0);
            NSEvent *event = [NSEvent eventWithCGEvent:cg];
            assert(event.hasPreciseScrollingDeltas == (wanted == 1));
            [NSApp sendEvent:event];
            assert(precise() == wanted);
            CFRelease(cg);
        }
        shutdown();
        assert(precise() == 0);
        shutdown();
        assert(initialize() == 1);
        shutdown();
        // Unity keeps native plugins loaded for the process lifetime.
        // AppKit may still release removed observer blocks when its pool drains.
        puts("Native macOS local-monitor tests passed: precise/discrete switching, idempotent initialization and cleanup.");
    }
    return 0;
}
