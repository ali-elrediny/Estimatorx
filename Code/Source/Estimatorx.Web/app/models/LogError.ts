﻿/// <reference path="../_ref.ts" />
module Estimatorx {
  "use strict";

  export interface ILogError {
    Message?: string;
    BaseMessage?: string;
    Text?: string;
    Type?: string;
    Source?: string;
    MethodName?: string;
    ModuleName?: string;
    ModuleVersion?: string;
    ErrorCode?: number;
  }
}
