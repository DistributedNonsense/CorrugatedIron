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

namespace CorrugatedIron.Models.Index
{
    public class RiakIndexKeyTerm
    {
        public string Key { get; private set; }
        public string Term { get; private set; }

        public RiakIndexKeyTerm(string key)
        {
            Key = key;
        }

        public RiakIndexKeyTerm(string key, string term) : this(key)
        {
            Term = term;
        }
    }
}
