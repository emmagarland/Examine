﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Examine;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.Collections;
using Lucene.Net.Documents;

namespace UmbracoExamine
{
    /// <summary>
    /// 
    /// </summary>
    public class SearchResults : ISearchResults
    {
        private IndexSearcher searcher;
        private AllHitsCollector collector;

        internal SearchResults(Query query, IEnumerable<SortField> sortField, IndexSearcher searcher)
        {
            //searcher = new IndexSearcher(new SimpleFSDirectory(examineSearcher.LuceneIndexFolder), true);
            this.searcher = searcher;
            this.searcher.SetDefaultFieldSortScoring(true, true);
            DoSearch(query, sortField);
        }

        private void DoSearch(Query query, IEnumerable<SortField> sortField)
        {
            if (sortField.Count() == 0)
            {
                var topDocs = searcher.Search(query, null, searcher.MaxDoc(), new Sort());
                collector = new AllHitsCollector(topDocs.scoreDocs);
                topDocs = null;
            }
            else
            {
                var topDocs = searcher.Search(query, null, searcher.MaxDoc(), new Sort(sortField.ToArray()));
                collector = new AllHitsCollector(topDocs.scoreDocs);
                topDocs = null;
            }
            this.TotalItemCount = collector.Count;
        }

        /// <summary>
        /// Gets the total number of results for the search
        /// </summary>
        /// <value>The total item count.</value>
        public int TotalItemCount { get; private set; }

        /// <summary>
        /// Internal cache of search results
        /// </summary>
        protected Dictionary<int, SearchResult> docs = new Dictionary<int, SearchResult>();

        /// <summary>
        /// Creates the search result from a <see cref="Lucene.Net.Documents.Document"/>
        /// </summary>
        /// <param name="doc">The doc to convert.</param>
        /// <param name="score">The score.</param>
        /// <returns>A populated search result object</returns>
        protected SearchResult CreateSearchResult(Document doc, float score)
        {
            string id = doc.Get("id");
            if (string.IsNullOrEmpty(id))
            {
                id = doc.Get(LuceneExamineIndexer.IndexNodeIdFieldName);
            }
            var sr = new SearchResult()
            {
                Id = int.Parse(id),
                Score = score
            };

            //we can use lucene to find out the fields which have been stored for this particular document
            //I'm not sure if it'll return fields that have null values though
            var fields = doc.GetFields();
            //ignore our internal fields though
            foreach (Field field in fields
                .Cast<Field>()
                .Where(x => x.Name() != LuceneExamineIndexer.IndexNodeIdFieldName && x.Name() != LuceneExamineIndexer.IndexTypeFieldName))
            {
                sr.Fields.Add(field.Name(), doc.Get(field.Name()));
            }

            return sr;
        }

        /// <summary>
        /// Skips to a particular point in the search results.
        /// </summary>
        /// <remarks>
        /// This allows for lazy loading of the results paging. We don't go into Lucene until we have to.
        /// </remarks>
        /// <param name="skip">The skip.</param>
        /// <returns></returns>
        public IEnumerable<SearchResult> Skip(int skip)
        {
            for (int i = skip; i < this.TotalItemCount; i++)
            {
                //first check our own cache to make sure it's not there
                if (!docs.ContainsKey(i))
                {
                    var docId = collector.GetDocId(i);
                    var doc = searcher.Doc(docId);
                    var score = collector.GetDocScore(i);

                    docs.Add(i, CreateSearchResult(doc, score));
                }
                //using yield return means if the user breaks out we wont keep going
                //only load what we need to load!
                //and we'll get it from our cache, this means you can go 
                //forward/ backwards without degrading performance
                yield return docs[i];
            }
        }

        #region IEnumerable<SearchResult> Members

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<SearchResult> GetEnumerator()
        {
            //if we're going to Enumerate from this itself we're not going to be skipping
            //so unless we made it IQueryable we can't do anything better than start from 0
            return this.Skip(0).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion
    }
}