﻿// Copyright (c) 2011 - OJ Reeves & Jeremiah Peschka
//
// This file is provided to you under the Apache License,
// Version 2.0 (the "License"); you may not use this file
// except in compliance with the License.  You may obtain
// a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using CorrugatedIron.Models.MapReduce.Languages;
using CorrugatedIron.Models.MapReduce.Phases;

namespace CorrugatedIron.Models.MapReduce.Fluent
{
    public class RiakFluentActionPhaseJavascript
    {
        private readonly RiakActionPhase<RiakPhaseLanguageJavascript> _phase;

        internal RiakFluentActionPhaseJavascript(RiakActionPhase<RiakPhaseLanguageJavascript> phase)
        {
            _phase = phase;
        }

        public RiakFluentActionPhaseJavascript Keep(bool keep)
        {
            _phase.Keep(keep);
            return this;
        }

        public RiakFluentActionPhaseJavascript Argument<T>(T argument)
        {
            _phase.Argument(argument);
            return this;
        }

        public RiakFluentActionPhaseJavascript Name(string name)
        {
            _phase.Language.Name(name);
            return this;
        }

        public RiakFluentActionPhaseJavascript Source(string source)
        {
            _phase.Language.Source(source);
            return this;
        }

        public RiakFluentActionPhaseJavascript BucketKey(string bucket, string key)
        {
            _phase.Language.BucketKey(bucket, key);
            return this;
        }
    }
}
