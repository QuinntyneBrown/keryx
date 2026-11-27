import { booleanAttribute, Component, input, output } from '@angular/core';
import type { ArticleDetail } from '@kx/api';
import { Alert, Badge, DetailPanel, Dl, Meter, type DlItem } from '@kx/components';

/** One article as the panel shows it: its words worked out by the Articles screen. */
export interface ArticlePanelView {
  id: string;
  detail: ArticleDetail;
  title: string;
  status: string;
  tone: 'success' | 'warning' | null;
  /** The original's address when it is `http(s)`, otherwise null. */
  url: string | null;
  organization: string;
  source: string;
  category: string;
  region: string;
  published: string;
  collected: string;
  relevance: string;
  alert: { tone: 'warning' | 'neutral'; text: string } | null;
}

/**
 * The open article (docs/mocks/pages/articles/selected*.html): the detail panel with its facts,
 * relevance, tags and the reason for its status. Articles draws it beside the list from 1024 px
 * and in a modal dialog below; it takes its view as an input either way.
 */
@Component({
  selector: 'kx-article-panel',
  imports: [Alert, Badge, DetailPanel, Dl, Meter],
  templateUrl: './article-panel.html',
  styleUrl: './article-panel.scss',
})
export class ArticlePanel {
  readonly shown = input.required<ArticlePanelView>();
  readonly focusOnOpen = input(false, { transform: booleanAttribute });
  readonly closed = output<void>();

  protected readonly facts: DlItem[] = [
    { key: 'organization', term: 'Organisation' },
    { key: 'source', term: 'Source' },
    { key: 'category', term: 'Category' },
    { key: 'region', term: 'Region' },
    { key: 'published', term: 'Published' },
    { key: 'collected', term: 'Collected' },
    { key: 'relevance', term: 'Relevance' },
    { key: 'tags', term: 'Tags' },
  ];
}
