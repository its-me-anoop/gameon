#import <AppKit/AppKit.h>
#include <atomic>

static id scrollMonitor = nil;
static std::atomic<int> preciseScroll{0};

extern "C" {
__attribute__((visibility("default"))) int Clinic_InitializeScrollMonitor(void) {
    void (^install)(void) = ^{
        if (scrollMonitor != nil) return;
        scrollMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskScrollWheel
            handler:^NSEvent *(NSEvent *event) {
                preciseScroll.store(event.hasPreciseScrollingDeltas ? 1 : 0, std::memory_order_relaxed);
                return event;
            }];
    };
    if (NSThread.isMainThread) install();
    else dispatch_sync(dispatch_get_main_queue(), install);
    return scrollMonitor != nil ? 1 : 0;
}

__attribute__((visibility("default"))) int Clinic_IsPreciseScroll(void) {
    return preciseScroll.load(std::memory_order_relaxed);
}

__attribute__((visibility("default"))) void Clinic_ShutdownScrollMonitor(void) {
    void (^remove)(void) = ^{
        if (scrollMonitor != nil) [NSEvent removeMonitor:scrollMonitor];
        scrollMonitor = nil;
        preciseScroll.store(0, std::memory_order_relaxed);
    };
    if (NSThread.isMainThread) remove();
    else dispatch_sync(dispatch_get_main_queue(), remove);
}
}
