// Calls FLDAttribution in FloadAttributionBridge.framework the way the .NET for
// iOS binding does: objc_msgSend on the class with the selector string from
// ApiDefinition.cs. Built and run on a simulator by native/ios/simulator-check.sh.
//   main <endpoint> <configure selector> <wrapper name> <wrapper version> <user id>
#import <Foundation/Foundation.h>
#import <objc/message.h>
#import <objc/runtime.h>
#include <unistd.h>

typedef void (*Configure)(id, SEL, NSString *, NSString *, NSInteger, NSString *, NSString *);
typedef void (*SetUserId)(id, SEL, NSString *);
typedef NSString *(*Getter)(id, SEL);

int main(int argc, char **argv) {
    @autoreleasepool {
        if (argc < 6) {
            fprintf(stderr, "usage: main <endpoint> <selector> <wrapper name> <wrapper version> <user id>\n");
            return 2;
        }
        Class fld = objc_getClass("FLDAttribution");
        if (fld == Nil) {
            fprintf(stderr, "FAIL FLDAttribution is not in the framework\n");
            return 1;
        }
        SEL configure = sel_registerName(argv[2]);
        if (![fld respondsToSelector:configure]) {
            fprintf(stderr, "FAIL FLDAttribution does not answer +%s\n", argv[2]);
            return 1;
        }
        NSString *version = ((Getter)objc_msgSend)(fld, sel_registerName("sdkVersion"));
        printf("native sdkVersion: %s\n", version.UTF8String);

        ((Configure)objc_msgSend)(fld, configure, @"fload_pk_test_local", @(argv[1]), 4, @(argv[3]), @(argv[4]));
        ((SetUserId)objc_msgSend)(fld, sel_registerName("setUserId:"), @(argv[5]));

        NSString *reference = nil;
        for (int i = 0; i < 50 && reference == nil; i++) {
            usleep(100 * 1000);
            reference = ((Getter)objc_msgSend)(fld, sel_registerName("installReference"));
        }
        if (reference == nil) {
            fprintf(stderr, "FAIL no install reference after configure\n");
            return 1;
        }
        printf("install reference: %s\n", reference.UTF8String);
        sleep(5);  // the SDK's own queue sends the install and the identify
        return 0;
    }
}
