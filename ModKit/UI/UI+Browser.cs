﻿using JetBrains.Annotations;
using ModKit.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using ToyBox;
using UnityEngine;

namespace ModKit {

    public static partial class UI {
        public class Browser<Item, Definition> {
            // Simple browser that displays a searchable collection of items along with a collection of available definitions.
            // It provides a toggle to show the definitions mixed in with the items. 
            // By default it shows just the title but you can provide an optional RowUI to show more complex row UI including buttons to add and remove items and so forth. This will be layed out after the title
            // You an also provide UI that renders children below the row
            public Settings Settings => Main.settings; // FIXME - move these settings into ModKit. Can't have dependency on ToyBox
            private IEnumerable<Definition> _pagedResults = new List<Definition>();
            public IEnumerable<Definition> filteredOrderedDefinitions;
            private Dictionary<Definition, Item> _currentDict;
            private readonly Dictionary<string, bool> _disclosureStates = new();
            private string _searchText = "";
            public bool SearchAsYouType;
            public bool ShowAll;
            public int SearchLimit;
            private int _pageCount;
            private int _matchCount;
            private int _currentPage = 1;
            private DateTime _lockedUntil;
            public bool needsReloadData = true;
            public void ReloadData() => needsReloadData = true;
            private bool _updatePages = false;
            private bool _startedLoading = false;
            private readonly bool _availableIsStatic;
            private List<Definition> _availableCache;
            string _prevCallerKey = String.Empty;
            public void OnShowGUI() => needsReloadData = true;
            public Browser(bool searchAsYouType = true, bool availableIsStatic = false, int searchLimit = -1) {
                SearchAsYouType = searchAsYouType;
                _availableIsStatic = availableIsStatic;
                Mod.NotifyOnShowGUI += OnShowGUI;
                if (searchLimit > 0)
                    SearchLimit = searchLimit;
                else
                    SearchLimit = Settings.searchLimit;
            }

            public void OnGUI(
                string callerKey,
                IEnumerable<Item> current,
                Func<IEnumerable<Definition>> available,    // Func because available may be slow
                Func<Item, Definition> definition,
                Func<Definition, string> title,
                Func<Definition, string> searchKey = null,
                Func<Definition, string> sortKey = null,
                Action onHeaderGUI = null,
                Action<Item, Definition> onRowGUI = null,
                Action<Item, Definition> onDetailGUI = null,
                Func<Item, Definition, Action<Item, Definition>> onChildrenGUI = null,
                int indent = 50,
                bool showDiv = true,
                bool search = true,
                float titleMinWidth = 100,
                float titleMaxWidth = 300,
                string searchTextPassedFromParent = "",
                bool showItemDiv = false
                ) {
                if (callerKey != _prevCallerKey) {
                    _prevCallerKey = callerKey;
                    _disclosureStates.Clear();
                }
                searchKey ??= title;
                sortKey ??= title;
                current ??= new List<Item>();
                List<Definition> definitions = Update(current, available, title, search, searchKey, sortKey, definition);
                if (search || SearchLimit < _matchCount) {
                    if (search) {
                        using (HorizontalScope()) {
                            indent.space();
                            ActionTextField(ref _searchText, "searchText", (text) => {
                                if (!SearchAsYouType) return;
                                _lockedUntil = DateTime.Now.AddMilliseconds(250);
                                needsReloadData = true;
                            }, () => { needsReloadData = true; }, width(320));
                            25.space();
                            Label("Limit", ExpandWidth(false));
                            ActionIntTextField(ref SearchLimit, "Search Limit", (i) => { _updatePages = true; }, () => { _updatePages = true; }, width(175));
                            if (SearchLimit > 1000) { SearchLimit = 1000; }
                            25.space();
                            _startedLoading |= DisclosureToggle("Show All".Orange().Bold(), ref ShowAll, () => ReloadData());
                            25.space();
                        }
                    } else {
                        if (_searchText != searchTextPassedFromParent) {
                            needsReloadData = true;
                            _searchText = searchTextPassedFromParent;
                            if (_searchText == null) {
                                _searchText = "";
                            }
                        }
                    }
                    using (HorizontalScope()) {
                        if (search) {
                            space(indent);
                            ActionButton("Search", () => { needsReloadData = true; }, AutoWidth());
                        }
                        space(25);
                        if (_matchCount > 0 || _searchText.Length > 0) {
                            var matchesText = "Matches: ".Green().Bold() + $"{_matchCount}".Orange().Bold();
                            if (_matchCount > SearchLimit) { matchesText += " => ".Cyan() + $"{SearchLimit}".Cyan().Bold(); }

                            Label(matchesText, ExpandWidth(false));
                        }
                        if (_matchCount > SearchLimit) {
                            string pageLabel = "Page: ".orange() + _currentPage.ToString().cyan() + " / " + _pageCount.ToString().cyan();
                            25.space();
                            Label(pageLabel, ExpandWidth(false));
                            ActionButton("-", () => {
                                if (_currentPage >= 1) {
                                    if (_currentPage == 1) {
                                        _currentPage = _pageCount;
                                    } else {
                                        _currentPage -= 1;
                                    }
                                    _updatePages = true;
                                }
                            }, AutoWidth());
                            ActionButton("+", () => {
                                if (_currentPage > _pageCount) return;
                                if (_currentPage == _pageCount) {
                                    _currentPage = 1;
                                } else {
                                    _currentPage += 1;
                                }
                                _updatePages = true;
                            }, AutoWidth());
                        }
                    }
                }
                if (showDiv)
                    Div(indent);
                if (onHeaderGUI != null) {
                    using (HorizontalScope(AutoWidth())) {
                        space(indent);
                        onHeaderGUI();
                    }
                }
                foreach (var def in definitions) {
                    if (showItemDiv) {
                        Div(indent);
                    }
                    var name = title(def);
                    var nameLower = name.ToLower();
                    _currentDict.TryGetValue(def, out var item);
                    var remainingWidth = ummWidth;
                    var showChildren = false;
                    var childGUI = onChildrenGUI?.Invoke(item, def);
                    using (HorizontalScope(AutoWidth())) {
                        space(indent);
                        remainingWidth -= indent;
                        var titleWidth = (remainingWidth / (IsWide ? 3.5f : 4.0f)) - 100;
                        var text = title(def);
                        var titleKey = $"{callerKey}-{text}";
                        if (item != null) {
                            text = text.Cyan().Bold();
                        }
                        if (childGUI == null) {
                            Label(text, width((int)titleWidth));
                        } else {
                            _disclosureStates.TryGetValue(titleKey, out showChildren);
                            if (DisclosureToggle(text, ref showChildren, titleWidth)) {
                                _disclosureStates.Clear();
                                _disclosureStates[titleKey] = showChildren;
                                needsReloadData = true;
                            }
                        }
                        var lastRect = GUILayoutUtility.GetLastRect();
                        remainingWidth -= lastRect.width;
                        10.space();
                        onRowGUI?.Invoke(item, def);
                    }
                    onDetailGUI?.Invoke(item, def);
                    if (showChildren) {
                        childGUI(item, def);
                    }
                }
            }

            private List<Definition> Update(IEnumerable<Item> current, Func<IEnumerable<Definition>> available, Func<Definition, string> title, bool search,
                Func<Definition, string> searchKey, Func<Definition, string> sortKey, Func<Item, Definition> definition) {
                if (Event.current.type == EventType.Layout) {
                    if (_startedLoading) {
                        _availableCache = available()?.ToList();
                        if (_availableCache?.Count() > 0) {
                            _startedLoading = false;
                            needsReloadData = true;
                            if (!_availableIsStatic) {
                                _availableCache = null;
                            }
                        }
                    }
                    if (needsReloadData) {
                        if (DateTime.Now.CompareTo(_lockedUntil) < 0) return _pagedResults?.ToList();
                        _currentDict = current.ToDictionaryIgnoringDuplicates(definition, c => c);
                        IEnumerable<Definition> definitions;
                        if (ShowAll) {
                            if (_startedLoading) {
                                definitions = _currentDict.Keys.ToList();
                            } else if (_availableIsStatic) {
                                definitions = _availableCache;
                            } else {
                                definitions = available();
                            }
                        } else {
                            definitions = _currentDict.Keys.ToList();
                        }
                        UpdateSearchResults(_searchText, definitions, searchKey, sortKey, title, search);
                        needsReloadData = false;
                    } else if (_updatePages) {
                        _updatePages = false;
                        UpdatePageCount();
                        UpdatePaginatedResults();
                    }
                }
                return _pagedResults?.ToList();
            }

            [UsedImplicitly]
            public void UpdateSearchResults(string searchTextParam,
                IEnumerable<Definition> definitions,
                Func<Definition, string> searchKey,
                Func<Definition, string> sortKey,
                Func<Definition, string> title,
                bool search
                ) {
                if (definitions == null) {
                    return;
                }
                var filtered = new List<Definition>();
                var terms = searchTextParam.Split(' ').Select(s => s.ToLower()).ToHashSet();

                if (search) {
                    foreach (var def in definitions) {
                        var name = title(def);
                        var nameLower = name.ToLower();
                        // Should items without name still be supported?
                        if (name is not { Length: > 0 }) {
                            continue;
                        }
                        if (def.GetType().ToString().Contains(searchTextParam)
                           ) {
                            filtered.Add(def);
                        } else if (searchKey != null) {
                            var text = searchKey(def).ToLower();
                            if (terms.All(term => text.Matches(term))) {
                                filtered.Add(def);
                            }
                        }
                    }
                } else {
                    filtered = definitions.ToList();
                }
                _matchCount = filtered.Count;
                filteredOrderedDefinitions = filtered.OrderBy(sortKey);
                UpdatePageCount();
                UpdatePaginatedResults();
                _updatePages = false;
            }
            public void UpdatePageCount() {
                if (SearchLimit > 0) {
                    _pageCount = (int)Math.Ceiling((double)_matchCount / SearchLimit);
                    _currentPage = Math.Min(_currentPage, _pageCount);
                    _currentPage = Math.Max(1, _currentPage);
                } else {
                    _pageCount = 1;
                    _currentPage = 1;
                }
            }
            public void UpdatePaginatedResults() {
                var limit = SearchLimit;
                var count = _matchCount;
                var offset = Math.Min(count, (_currentPage - 1) * limit);
                limit = Math.Min(limit, Math.Max(count, count - limit));
                Mod.Trace($"{_currentPage} / {_pageCount} count: {count} => offset: {offset} limit: {limit} ");
                _pagedResults = filteredOrderedDefinitions.Skip(offset).Take(limit).ToArray();
            }
        }
    }
}